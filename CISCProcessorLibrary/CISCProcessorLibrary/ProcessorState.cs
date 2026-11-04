namespace CISCProcessorLibrary;

/// <summary>Immutable snapshot of every architectural register of the processor.</summary>
public sealed record ProcessorState(
    byte Accumulator,
    IReadOnlyList<byte> Registers,
    ushort InstructionPointer,
    ushort StackPointer,
    ushort BasePointer,
    ushort SourceIndex,
    ushort DestinationIndex,
    byte FlagRegister,
    byte InstructionRegister,
    ushort MemoryAddressRegister,
    byte MemoryDataRegister,
    byte PendingInterrupts,
    CyclePhase Phase,
    long Cycles,
    long InstructionsExecuted,
    bool Halted,
    bool Waiting,
    FaultKind Fault)
{
    /// <summary>Returns whether <paramref name="flag"/> is set in <see cref="FlagRegister"/>.</summary>
    public bool IsFlagSet(Flag flag) => (FlagRegister & (1 << (int)flag)) != 0;

    /// <summary>Flags rendered as "ZCNVI" with a dash for each clear flag, for example "Z-N-I".</summary>
    public string FlagsText => FormatFlags(FlagRegister);

    /// <summary>Formats a raw flag register value as "ZCNVI" with dashes for clear flags.</summary>
    public static string FormatFlags(byte flags)
    {
        Span<char> text = stackalloc char[5];
        text[0] = (flags & (1 << (int)Flag.Zero)) != 0 ? 'Z' : '-';
        text[1] = (flags & (1 << (int)Flag.Carry)) != 0 ? 'C' : '-';
        text[2] = (flags & (1 << (int)Flag.Negative)) != 0 ? 'N' : '-';
        text[3] = (flags & (1 << (int)Flag.Overflow)) != 0 ? 'V' : '-';
        text[4] = (flags & (1 << (int)Flag.Interrupt)) != 0 ? 'I' : '-';
        return new string(text);
    }
}
