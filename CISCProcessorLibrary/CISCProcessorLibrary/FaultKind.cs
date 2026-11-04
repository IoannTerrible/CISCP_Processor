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

    /// <summary>A pop (or RET / IRET / LEAVE) happened while the stack was empty.</summary>
    StackUnderflow,

    /// <summary>DIV by a zero divisor.</summary>
    DivideByZero,

    /// <summary>DIV produced a quotient that does not fit in 8 bits (the 8086 "divide error").</summary>
    DivideOverflow,
}
