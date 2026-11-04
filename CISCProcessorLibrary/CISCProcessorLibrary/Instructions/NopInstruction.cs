namespace CISCProcessorLibrary.Instructions;

/// <summary>NOP - does nothing for one cycle.</summary>
public sealed class NopInstruction : Instruction
{
    public NopInstruction(byte opcode)
        : base(opcode, "NOP", 1, "No operation")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
    }
}
