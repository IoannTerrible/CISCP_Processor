namespace CISCProcessorLibrary;

/// <summary>
/// Raised inside the data path when an instruction cannot complete.
/// <see cref="CISCProcessor.Step"/> catches it, halts the processor and records the fault,
/// so callers normally observe faults through <see cref="CISCProcessor.Fault"/> rather than this exception.
/// </summary>
public sealed class ProcessorFaultException : Exception
{
    public ProcessorFaultException(FaultKind fault, int address, string message)
        : base(message)
    {
        Fault = fault;
        Address = address;
    }

    /// <summary>What went wrong.</summary>
    public FaultKind Fault { get; }

    /// <summary>Address of the instruction that was executing when the fault happened.</summary>
    public int Address { get; }
}
