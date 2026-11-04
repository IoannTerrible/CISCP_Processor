using CISCProcessorLibrary;
using Xunit;

namespace CISCProcessorLibrary.Tests;

public class DisassemblerTests
{
    public static IEnumerable<object[]> AllOpcodes() =>
        InstructionSet.Default.All.Select(i => new object[] { i.Opcode });

    [Theory]
    [MemberData(nameof(AllOpcodes))]
    public void EveryInstruction_RoundTripsThroughDisassemblerAndAssembler(byte opcode)
    {
        IInstruction instruction = InstructionSet.Default.Decode(opcode)!;
        var memory = new Memory();
        memory[0] = opcode;
        int offset = 1;
        foreach (OperandSlot slot in instruction.Syntax.Operands)
        {
            switch (slot.Kind)
            {
                case OperandKind.Register:
                case OperandKind.RegisterIndirect:
                case OperandKind.AddressRegister:
                case OperandKind.AddressRegisterIndirect:
                    memory[offset++] = 1;
                    break;
                case OperandKind.Indexed:
                    memory[offset++] = 0x42;
                    memory[offset++] = 0x01;
                    memory[offset++] = 1;
                    break;
                case OperandKind.AddressRegisterDisplaced:
                    memory[offset++] = 2;
                    memory[offset++] = 0xFE;     // [BP-2]
                    break;
                case OperandKind.Immediate:
                    memory[offset++] = 0x42;
                    break;
                case OperandKind.Immediate16:
                case OperandKind.Address:
                case OperandKind.Direct:
                    memory[offset++] = 0x42;
                    memory[offset++] = 0x01;
                    break;
            }
        }

        DisassembledInstruction decoded = Disassembler.Disassemble(memory, 0);

        Assert.Same(instruction, decoded.Instruction);
        Assert.Equal(instruction.Length, decoded.Length);
        byte[] reassembled = Assembler.Assemble(decoded.Text).Listing[0].Bytes;
        Assert.Equal(decoded.Bytes, reassembled);
    }

    [Fact]
    public void InvalidOpcode_BecomesADataByte()
    {
        var memory = new Memory();
        memory[5] = 0xEE;

        DisassembledInstruction decoded = Disassembler.Disassemble(memory, 5);

        Assert.Null(decoded.Instruction);
        Assert.Equal(1, decoded.Length);
        Assert.Equal(".db 0xEE", decoded.Text);
    }

    [Fact]
    public void DisassembleRange_WalksVariableLengthInstructions()
    {
        AssembledProgram program = Assembler.Assemble("MOV A, [0x80+R1]\nINC A\nMOV A, [BP-1]\nJMP 0\nREP MOVSB\nHLT");
        var memory = new Memory();
        memory.Load(program.ToImage());

        List<DisassembledInstruction> items = Disassembler.DisassembleRange(memory, 0, program.Length).ToList();

        Assert.Equal(new[] { "MOV A, [0x0080+R1]", "INC A", "MOV A, [BP-1]", "JMP 0x0000", "REP MOVSB", "HLT" }, items.Select(i => i.Text));
        Assert.Equal(new[] { 0, 4, 5, 8, 11, 12 }, items.Select(i => i.Address));
    }

    [Fact]
    public void InstructionSet_HasNoDuplicateSyntaxAndCoversEveryFamily()
    {
        IReadOnlyList<IInstruction> all = InstructionSet.Default.All;

        Assert.Equal(all.Count, all.Select(i => i.Syntax.Template).Distinct().Count());
        Assert.Equal(all.Count, all.Select(i => i.Opcode).Distinct().Count());
        Assert.Contains(all, i => i.Mnemonic == "LOOP");
        Assert.Contains(all, i => i.Mnemonic == "MUL");
        Assert.Contains(all, i => i.Mnemonic == "REP");
        Assert.Contains(all, i => i.Mnemonic == "IRET");
        Assert.Contains(all, i => i.Mnemonic == "ENTER");
        Assert.Contains(all, i => i.Syntax.Template == "MOV A, [addr+Rn]");
        Assert.Contains(all, i => i.Syntax.Template == "JMP [XR]");
        Assert.Equal(111, all.Count);
    }
}
