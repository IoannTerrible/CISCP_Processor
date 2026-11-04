namespace CISCProcessorLibrary;

/// <summary>Why <see cref="CISCProcessor.Run"/> returned.</summary>
public enum StopReason
{
    /// <summary>A HLT instruction was executed with interrupts disabled.</summary>
    Halted,

    /// <summary>The processor raised a fault (see <see cref="RunResult.Fault"/>).</summary>
    Fault,

    /// <summary>The step budget passed to Run was exhausted (an endless loop, or waiting for an interrupt that never comes).</summary>
    StepLimit,
}

/// <summary>Summary of a <see cref="CISCProcessor.Run"/> call.</summary>
public sealed record RunResult(
    StopReason Reason,
    long InstructionsExecuted,
    long Cycles,
    FaultKind Fault,
    string? FaultMessage,
    ProcessorState FinalState);
