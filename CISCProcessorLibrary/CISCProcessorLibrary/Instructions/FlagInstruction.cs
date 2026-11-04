namespace CISCProcessorLibrary.Instructions;

/// <summary>CLC / STC - clears or sets the carry flag directly.</summary>
public sealed class FlagInstruction : Instruction
{
    public FlagInstruction(byte opcode, string syntax, Flag flag, bool value, string description)
        : base(opcode, syntax, 1, description)
    {
        Flag = flag;
        Value = value;
    }

    public Flag Flag { get; }

    public bool Value { get; }

    public override void Execute(CISCProcessor cpu, Operands operands) => cpu.SetFlag(Flag, Value);
}
