namespace CISCProcessorLibrary.Instructions;

/// <summary>POP A / Rn / FLAGS - loads the operand from the hardware stack.</summary>
public sealed class PopInstruction : Instruction
{
    public PopInstruction(byte opcode, string syntax, string description)
        : base(opcode, syntax, 3, description)
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) =>
        WriteOperand(cpu, Slot(0), operands, cpu.Pop());
}
