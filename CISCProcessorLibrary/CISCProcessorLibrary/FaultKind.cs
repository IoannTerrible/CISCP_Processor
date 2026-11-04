namespace CISCProcessorLibrary;

/// <summary>Hardware faults that stop the processor.</summary>
public enum FaultKind
{
    /// <summary>No fault.</summary>
    None,

    /// <summary>The byte fetched from memory does not encode any instruction.</summary>
    InvalidOpcode,

    /// <summary>A push happened while the stack pointer was already at the bottom of memory.</summary>
    StackOverflow,

    /// <summary>A pop (or RET) happened while the stack was empty.</summary>
    StackUnderflow,

    /// <summary>DIV or MOD by a zero divisor.</summary>
    DivideByZero,
}
