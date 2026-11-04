namespace CISCProcessorLibrary;

/// <summary>What one call of <see cref="CISCProcessor.Step"/> did.</summary>
public enum StepKind
{
    /// <summary>A regular instruction was fetched, decoded and executed.</summary>
    Instruction,

    /// <summary>An interrupt request was acknowledged instead of fetching an instruction.</summary>
    Interrupt,

    /// <summary>The processor idled because it is waiting for an interrupt (after HLT with I set).</summary>
    Wait,
}

/// <summary>Everything the processor did during one step.</summary>
public sealed record StepTrace(
    StepKind Kind,
    int Address,
    byte[] Bytes,
    string Disassembly,
    int Cycles,
    IReadOnlyList<MicroOp> MicroOps,
    ProcessorState StateAfter)
{
    /// <summary>Instruction bytes rendered as hex, for example "19 80 01 01".</summary>
    public string BytesText => string.Join(' ', Bytes.Select(b => b.ToString("X2")));

    public override string ToString() => $"{Address:X4}  {BytesText,-11}  {Disassembly,-22}  {Cycles} cycles";
}
