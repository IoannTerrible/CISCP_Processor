namespace CISCProcessorLibrary;

/// <summary>Everything the processor did while executing one instruction.</summary>
public sealed record StepTrace(
    int Address,
    byte[] Bytes,
    string Disassembly,
    int Cycles,
    IReadOnlyList<MicroOp> MicroOps,
    ProcessorState StateAfter)
{
    /// <summary>Instruction bytes rendered as hex, for example "19 80 01".</summary>
    public string BytesText => string.Join(' ', Bytes.Select(b => b.ToString("X2")));

    public override string ToString() => $"{Address:X2}  {BytesText,-8}  {Disassembly,-20}  {Cycles} cycles";
}
