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
        // Operand bytes: registers get index 1, numbers get 0x42; both are valid for every slot.
        int offset = 1;
        foreach (OperandSlot slot in instruction.Syntax.Operands)
        {
            switch (slot.Kind)
            {
                case OperandKind.Register:
                case OperandKind.RegisterIndirect:
                    memory[offset++] = 1;
                    break;
                case OperandKind.Indexed:
                    memory[offset++] = 0x42;
                    memory[offset++] = 1;
                    break;
                case OperandKind.Immediate:
                case OperandKind.Address:
                case OperandKind.Direct:
                    memory[offset++] = 0x42;
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
        AssembledProgram program = Assembler.Assemble("MOV A, [0x80+R1]\nINC A\nJMP 0\nHLT");
        var memory = new Memory();
        memory.Load(program.ToImage());

        List<DisassembledInstruction> items = Disassembler.DisassembleRange(memory, 0, program.Length).ToList();

        Assert.Equal(new[] { "MOV A, [0x80+R1]", "INC A", "JMP 0x00", "HLT" }, items.Select(i => i.Text));
        Assert.Equal(new[] { 0, 3, 4, 6 }, items.Select(i => i.Address));
    }

    [Fact]
    public void InstructionSet_HasNoDuplicateSyntaxAndCoversEveryFamily()
    {
        IReadOnlyList<IInstruction> all = InstructionSet.Default.All;

        Assert.Equal(all.Count, all.Select(i => i.Syntax.Template).Distinct().Count());
        Assert.Equal(all.Count, all.Select(i => i.Opcode).Distinct().Count());
        Assert.Contains(all, i => i.Mnemonic == "LOOP");
        Assert.Contains(all, i => i.Mnemonic == "MUL");
        Assert.Contains(all, i => i.Syntax.Template == "MOV A, [addr+Rn]");
        Assert.True(all.Count >= 70);
    }
}
