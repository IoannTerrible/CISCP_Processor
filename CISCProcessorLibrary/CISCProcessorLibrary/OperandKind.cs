namespace CISCProcessorLibrary;

/// <summary>Addressing modes an instruction operand can use.</summary>
public enum OperandKind
{
    /// <summary>The accumulator A. Encodes no bytes.</summary>
    Accumulator,

    /// <summary>A general purpose 8 bit register R0..R7. Encodes one byte: the register index.</summary>
    Register,

    /// <summary>A 16 bit address register SI, DI, BP or SP. Encodes one byte: 0 = SI, 1 = DI, 2 = BP, 3 = SP.</summary>
    AddressRegister,

    /// <summary>An 8 bit constant embedded in the instruction (imm8 / port). Encodes one byte.</summary>
    Immediate,

    /// <summary>A 16 bit constant embedded in the instruction (imm16), little-endian. Encodes two bytes.</summary>
    Immediate16,

    /// <summary>An absolute 16 bit code address used by jumps and calls. Encodes two bytes.</summary>
    Address,

    /// <summary>Direct addressing: the byte (or word) in memory at a 16 bit address ([addr]). Encodes two bytes.</summary>
    Direct,

    /// <summary>Zero page register indirect: the byte at address Rn, i.e. inside the first 256 bytes ([Rn]). Encodes one byte.</summary>
    RegisterIndirect,

    /// <summary>Indexed addressing: the byte at addr + Rn ([addr+Rn]). Encodes three bytes: address, register.</summary>
    Indexed,

    /// <summary>Address register indirect: the byte at the address held by SI, DI, BP or SP ([XR]). Encodes one byte.</summary>
    AddressRegisterIndirect,

    /// <summary>Based addressing: the byte at XR + signed 8 bit displacement ([XR+disp8]). Encodes two bytes: register, displacement.</summary>
    AddressRegisterDisplaced,

    /// <summary>The FLAGS register. Encodes no bytes.</summary>
    Flags,

    /// <summary>A literal word that is part of the mnemonic (the MOVSB in "REP MOVSB"). Encodes no bytes.</summary>
    Literal,
}

/// <summary>Helpers describing how each addressing mode is encoded.</summary>
public static class OperandKindExtensions
{
    /// <summary>Number of operand bytes the addressing mode occupies in the instruction stream.</summary>
    public static int ByteCount(this OperandKind kind) => kind switch
    {
        OperandKind.Accumulator => 0,
        OperandKind.Flags => 0,
        OperandKind.Literal => 0,
        OperandKind.Immediate16 => 2,
        OperandKind.Address => 2,
        OperandKind.Direct => 2,
        OperandKind.AddressRegisterDisplaced => 2,
        OperandKind.Indexed => 3,
        _ => 1,
    };

    /// <summary>True when a numeric literal or a label can be written for this operand in assembly source.</summary>
    public static bool IsNumeric(this OperandKind kind) =>
        kind is OperandKind.Immediate or OperandKind.Immediate16 or OperandKind.Address;

    /// <summary>True when the operand carries a 16 bit value (address registers and 16 bit immediates).</summary>
    public static bool IsWide(this OperandKind kind) =>
        kind is OperandKind.AddressRegister or OperandKind.Immediate16;

    /// <summary>True when the operand refers to a memory cell.</summary>
    public static bool IsMemory(this OperandKind kind) =>
        kind is OperandKind.Direct or OperandKind.RegisterIndirect or OperandKind.Indexed
            or OperandKind.AddressRegisterIndirect or OperandKind.AddressRegisterDisplaced;
}
