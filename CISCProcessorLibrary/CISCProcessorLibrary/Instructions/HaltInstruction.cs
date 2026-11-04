namespace CISCProcessorLibrary.Instructions;

/// <summary>HLT - stops the clock until the processor is reset.</summary>
public sealed class HaltInstruction : Instruction
{
    public HaltInstruction(byte opcode)
        : base(opcode, "HLT", 1, "Halt the processor")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) => cpu.Halt();
}
