namespace CISCProcessorLibrary.Instructions;

/// <summary>OUT port, A - writes the accumulator to an output port (0 = numbers, 1 = characters).</summary>
public sealed class OutInstruction : Instruction
{
    public OutInstruction(byte opcode)
        : base(opcode, "OUT port, A", 3, "Write A to an output port (0 = decimal, 1 = character)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) =>
        cpu.Output(ReadOperand(cpu, Slot(0), operands), cpu.Accumulator);
}
