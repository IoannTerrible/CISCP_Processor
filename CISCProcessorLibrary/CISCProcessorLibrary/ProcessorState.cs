namespace CISCProcessorLibrary;

/// <summary>Immutable snapshot of every architectural register of the processor.</summary>
public sealed record ProcessorState(
    byte Accumulator,
    IReadOnlyList<byte> Registers,
    byte InstructionPointer,
    byte StackPointer,
    byte FlagRegister,
    byte InstructionRegister,
    byte MemoryAddressRegister,
    byte MemoryDataRegister,
    CyclePhase Phase,
    long Cycles,
    long InstructionsExecuted,
    bool Halted,
    FaultKind Fault)
{
    /// <summary>Returns whether <paramref name="flag"/> is set in <see cref="FlagRegister"/>.</summary>
    public bool IsFlagSet(Flag flag) => (FlagRegister & (1 << (int)flag)) != 0;

    /// <summary>Flags rendered as "ZCNV" with a dash for each clear flag, for example "Z-N-".</summary>
    public string FlagsText => FormatFlags(FlagRegister);

    /// <summary>Formats a raw flag register value as "ZCNV" with dashes for clear flags.</summary>
    public static string FormatFlags(byte flags)
    {
        Span<char> text = stackalloc char[4];
        text[0] = (flags & (1 << (int)Flag.Zero)) != 0 ? 'Z' : '-';
        text[1] = (flags & (1 << (int)Flag.Carry)) != 0 ? 'C' : '-';
        text[2] = (flags & (1 << (int)Flag.Negative)) != 0 ? 'N' : '-';
        text[3] = (flags & (1 << (int)Flag.Overflow)) != 0 ? 'V' : '-';
        return new string(text);
    }
}
