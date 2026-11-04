namespace CISCProcessorLibrary;

/// <summary>Addressing modes an instruction operand can use.</summary>
public enum OperandKind
{
    /// <summary>The accumulator A. Encodes no bytes.</summary>
    Accumulator,

    /// <summary>A general purpose register R0..R7. Encodes one byte: the register index.</summary>
    Register,

    /// <summary>An 8 bit constant embedded in the instruction (imm8 / port). Encodes one byte.</summary>
    Immediate,

    /// <summary>An absolute code address used by jumps and calls. Encodes one byte.</summary>
    Address,

    /// <summary>Direct addressing: the byte in memory at the given address ([addr]). Encodes one byte.</summary>
    Direct,

    /// <summary>Register indirect addressing: the byte in memory whose address is held by Rn ([Rn]). Encodes one byte.</summary>
    RegisterIndirect,

    /// <summary>Indexed addressing: the byte in memory at addr + Rn ([addr+Rn]). Encodes two bytes.</summary>
    Indexed,

    /// <summary>The stack pointer SP. Encodes no bytes.</summary>
    StackPointer,

    /// <summary>The FLAGS register. Encodes no bytes.</summary>
    Flags,
}

/// <summary>Helpers describing how each addressing mode is encoded.</summary>
public static class OperandKindExtensions
{
    /// <summary>Number of operand bytes the addressing mode occupies in the instruction stream.</summary>
    public static int ByteCount(this OperandKind kind) => kind switch
    {
        OperandKind.Accumulator => 0,
        OperandKind.StackPointer => 0,
        OperandKind.Flags => 0,
        OperandKind.Indexed => 2,
        _ => 1,
    };

    /// <summary>True when a numeric literal or a label can be written for this operand in assembly source.</summary>
    public static bool IsNumeric(this OperandKind kind) =>
        kind is OperandKind.Immediate or OperandKind.Address;
}
