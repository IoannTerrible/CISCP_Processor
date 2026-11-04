namespace CISCProcessorLibrary;

/// <summary>
/// Bit positions inside the FLAGS (status) register.
/// The numeric value of each member is the bit index inside <see cref="CISCProcessor.FlagRegister"/>.
/// </summary>
public enum Flag
{
    /// <summary>Z - the last ALU result was zero.</summary>
    Zero = 0,

    /// <summary>C - unsigned carry out of (or borrow into) the top bit of the last ALU result.</summary>
    Carry = 1,

    /// <summary>N - top bit of the last ALU result (the sign of a two's complement value).</summary>
    Negative = 2,

    /// <summary>V - signed (two's complement) overflow of the last ALU result.</summary>
    Overflow = 3,

    /// <summary>I - maskable interrupts are enabled (EI sets it, DI and interrupt entry clear it).</summary>
    Interrupt = 4,
}
