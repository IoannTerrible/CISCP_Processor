namespace CISCProcessorLibrary;

/// <summary>
/// Bit positions inside the FLAGS (status) register.
/// The numeric value of each member is the bit index inside <see cref="CISCProcessor.FlagRegister"/>.
/// </summary>
public enum Flag
{
    /// <summary>Z - the last ALU result was zero.</summary>
    Zero = 0,

    /// <summary>C - unsigned carry out of (or borrow into) bit 7 of the last ALU result.</summary>
    Carry = 1,

    /// <summary>N - bit 7 of the last ALU result (the sign of a two's complement value).</summary>
    Negative = 2,

    /// <summary>V - signed (two's complement) overflow of the last ALU result.</summary>
    Overflow = 3,
}
