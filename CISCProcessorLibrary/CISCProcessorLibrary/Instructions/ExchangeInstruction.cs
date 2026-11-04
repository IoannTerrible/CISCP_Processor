namespace CISCProcessorLibrary.Instructions;

/// <summary>XCHG A, Rn - swaps the accumulator with a register in a single instruction.</summary>
public sealed class ExchangeInstruction : Instruction
{
    public ExchangeInstruction(byte opcode)
        : base(opcode, "XCHG A, Rn", 2, "Swap A and Rn")
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
