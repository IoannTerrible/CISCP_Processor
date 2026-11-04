namespace CISCProcessorLibrary.Instructions;

/// <summary>PUSH A / Rn / FLAGS - stores the operand on the hardware stack.</summary>
public sealed class PushInstruction : Instruction
{
    public PushInstruction(byte opcode, string syntax, string description)
        : base(opcode, syntax, 3, description)
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) =>
        cpu.Push(ReadOperand(cpu, Slot(0), operands));
}
