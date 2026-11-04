namespace CISCProcessorLibrary.Instructions;

/// <summary>
/// MOV destination, source - copies a byte or a word between registers, memory and immediates.
/// The syntax template decides the addressing modes and the width (a 16 bit transfer whenever one side
/// is an address register or a 16 bit immediate). Flags are not affected.
/// </summary>
public sealed class MoveInstruction : Instruction
{
    public MoveInstruction(byte opcode, string syntax, string description)
        : base(opcode, syntax, description)
    {
        Wide = Syntax.Operands.Any(s => s.Kind.IsWide());
    }

    /// <summary>True for a 16 bit transfer.</summary>
    public bool Wide { get; }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        if (Wide)
            WriteOperand16(cpu, Slot(0), operands, ReadOperand16(cpu, Slot(1), operands));
        else
            WriteOperand(cpu, Slot(0), operands, ReadOperand(cpu, Slot(1), operands));
    }
}

/// <summary>XCHG A, Rn - swaps the accumulator with a register in a single instruction.</summary>
public sealed class ExchangeInstruction : Instruction
{
    public ExchangeInstruction(byte opcode)
        : base(opcode, "XCHG A, Rn", "Swap A and Rn")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte a = cpu.Accumulator;
        byte r = ReadOperand(cpu, Slot(1), operands);
        cpu.Accumulator = r;
        WriteOperand(cpu, Slot(1), operands, a);
    }
}

/// <summary>LEA XR, [XS+disp8] - loads the effective address itself instead of the byte it points at.</summary>
public sealed class LoadEffectiveAddressInstruction : Instruction
{
    public LoadEffectiveAddressInstruction(byte opcode)
        : base(opcode, "LEA XR, [XS+disp8]", "XR = XS + disp8 (address arithmetic without a memory access)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        OperandSlot source = Slot(1);
        int address = cpu.ReadAddressRegister(operands[source.ByteOffset]) + (sbyte)operands[source.ByteOffset + 1];
        cpu.ChargeEffectiveAddress(source.Kind);
        cpu.WriteAddressRegister(operands[Slot(0).ByteOffset], (ushort)address);
    }
}
