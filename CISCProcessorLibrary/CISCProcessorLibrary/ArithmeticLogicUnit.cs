namespace CISCProcessorLibrary;

/// <summary>Binary operations the ALU can perform on the accumulator and a second operand.</summary>
public enum AluOperation
{
    Add,
    AddWithCarry,
    Subtract,
    SubtractWithBorrow,
    Compare,
    And,
    Or,
    Xor,
}

/// <summary>Single operand operations the ALU can perform in place.</summary>
public enum UnaryOperation
{
    Increment,
    Decrement,
    Not,
    Negate,
    ShiftLeft,
    ShiftRight,
    RotateLeft,
    RotateRight,
}

/// <summary>Operations on the 16 bit address registers.</summary>
public enum AddressOperation
{
    Add,
    Subtract,
    Compare,
    Increment,
    Decrement,
}

/// <summary>
/// The arithmetic logic unit: an 8 bit data path for A and R0..R7 plus a 16 bit path for the address
/// registers. Every operation returns the result and updates the Z, C, N and V flags of the owning
/// processor exactly like a classic 8 bit CPU would. The interrupt flag is never touched by the ALU.
/// </summary>
public sealed class ArithmeticLogicUnit
{
    private readonly CISCProcessor cpu;

    internal ArithmeticLogicUnit(CISCProcessor cpu)
    {
        this.cpu = cpu;
    }

    /// <summary>Performs a binary operation. For <see cref="AluOperation.Compare"/> only the flags matter.</summary>
    public byte Apply(AluOperation operation, byte a, byte b) => operation switch
    {
        AluOperation.Add => Add(a, b, useCarry: false),
        AluOperation.AddWithCarry => Add(a, b, useCarry: true),
        AluOperation.Subtract => Subtract(a, b, useBorrow: false, "SUB"),
        AluOperation.SubtractWithBorrow => Subtract(a, b, useBorrow: true, "SBB"),
        AluOperation.Compare => Subtract(a, b, useBorrow: false, "CMP"),
        AluOperation.And => Logic("AND", a, b, (byte)(a & b)),
        AluOperation.Or => Logic("OR", a, b, (byte)(a | b)),
        AluOperation.Xor => Logic("XOR", a, b, (byte)(a ^ b)),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };

    /// <summary>Performs a single operand operation.</summary>
    public byte Apply(UnaryOperation operation, byte a) => operation switch
    {
        UnaryOperation.Increment => Increment(a),
        UnaryOperation.Decrement => Decrement(a),
        UnaryOperation.Not => Logic("NOT", a, 0, (byte)~a),
        UnaryOperation.Negate => Negate(a),
        UnaryOperation.ShiftLeft => Shift("SHL", a, (byte)(a << 1), carry: (a & 0x80) != 0),
        UnaryOperation.ShiftRight => Shift("SHR", a, (byte)(a >> 1), carry: (a & 0x01) != 0),
        UnaryOperation.RotateLeft => Shift("ROL", a, (byte)((a << 1) | (a >> 7)), carry: (a & 0x80) != 0),
        UnaryOperation.RotateRight => Shift("ROR", a, (byte)((a >> 1) | (a << 7)), carry: (a & 0x01) != 0),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };

    /// <summary>Performs a 16 bit operation on an address register value.</summary>
    public ushort Apply(AddressOperation operation, ushort a, ushort b)
    {
        switch (operation)
        {
            case AddressOperation.Add:
            {
                int full = a + b;
                ushort result = (ushort)full;
                SetFlags16(result, carry: full > 0xFFFF, overflow: ((a ^ result) & (b ^ result) & 0x8000) != 0);
                return Finish("ADD16", a, b, result);
            }
            case AddressOperation.Subtract:
            case AddressOperation.Compare:
            {
                int full = a - b;
                ushort result = (ushort)full;
                SetFlags16(result, carry: full < 0, overflow: ((a ^ b) & (a ^ result) & 0x8000) != 0);
                return Finish(operation == AddressOperation.Compare ? "CMP16" : "SUB16", a, b, result);
            }
            case AddressOperation.Increment:
            {
                ushort result = (ushort)(a + 1);
                SetFlags16(result, carry: cpu.IsFlagSet(Flag.Carry), overflow: a == 0x7FFF);
                return Finish("INC16", a, 1, result);
            }
            case AddressOperation.Decrement:
            {
                ushort result = (ushort)(a - 1);
                SetFlags16(result, carry: cpu.IsFlagSet(Flag.Carry), overflow: a == 0x8000);
                return Finish("DEC16", a, 1, result);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    /// <summary>a + b (+ C). C = carry out of bit 7, V = signed overflow.</summary>
    public byte Add(byte a, byte b, bool useCarry)
    {
        int carryIn = useCarry && cpu.IsFlagSet(Flag.Carry) ? 1 : 0;
        int full = a + b + carryIn;
        byte result = (byte)full;
        SetFlags(result, carry: full > 0xFF, overflow: ((a ^ result) & (b ^ result) & 0x80) != 0);
        return Finish(useCarry ? "ADC" : "ADD", a, b, result);
    }

    /// <summary>a - b (- C). C = borrow (unsigned a &lt; b), V = signed overflow.</summary>
    public byte Subtract(byte a, byte b, bool useBorrow, string name = "SUB")
    {
        int borrowIn = useBorrow && cpu.IsFlagSet(Flag.Carry) ? 1 : 0;
        int full = a - b - borrowIn;
        byte result = (byte)full;
        SetFlags(result, carry: full < 0, overflow: ((a ^ b) & (a ^ result) & 0x80) != 0);
        return Finish(name, a, b, result);
    }

    /// <summary>
    /// A * Rn as a 16 bit product (R7:A, like AH:AL on the 8086). C and V are set when the high byte is
    /// not zero, Z and N describe the low byte.
    /// </summary>
    public ushort Multiply(byte a, byte b)
    {
        int product = a * b;
        byte low = (byte)product;
        SetFlags(low, carry: product > 0xFF, overflow: product > 0xFF);
        cpu.Record(MicroOp.Alu("MUL", a, b, product));
        cpu.ChargeAlu("MUL", wide: true);
        return (ushort)product;
    }

    /// <summary>
    /// Unsigned (R7:A) / Rn. Faults on a zero divisor and when the quotient does not fit in 8 bits,
    /// exactly like the 8086 divide error. Returns the quotient and the remainder.
    /// </summary>
    public (byte Quotient, byte Remainder) Divide(ushort dividend, byte divisor)
    {
        if (divisor == 0)
            cpu.RaiseFault(FaultKind.DivideByZero, "DIV by zero");
        int quotient = dividend / divisor;
        if (quotient > 0xFF)
            cpu.RaiseFault(FaultKind.DivideOverflow, $"DIV overflow: {dividend} / {divisor} = {quotient} does not fit in 8 bits");
        byte remainder = (byte)(dividend % divisor);
        SetFlags((byte)quotient, carry: false, overflow: false);
        cpu.Record(MicroOp.Alu("DIV", dividend, divisor, quotient));
        cpu.ChargeAlu("DIV", wide: true);
        return ((byte)quotient, remainder);
    }

    private byte Increment(byte a)
    {
        byte result = (byte)(a + 1);
        SetFlags(result, carry: cpu.IsFlagSet(Flag.Carry), overflow: a == 0x7F);
        return Finish("INC", a, 1, result);
    }

    private byte Decrement(byte a)
    {
        byte result = (byte)(a - 1);
        SetFlags(result, carry: cpu.IsFlagSet(Flag.Carry), overflow: a == 0x80);
        return Finish("DEC", a, 1, result);
    }

    private byte Negate(byte a)
    {
        byte result = (byte)(-a);
        SetFlags(result, carry: a != 0, overflow: a == 0x80);
        return Finish("NEG", a, 0, result);
    }

    private byte Logic(string name, byte a, byte b, byte result)
    {
        SetFlags(result, carry: false, overflow: false);
        return Finish(name, a, b, result);
    }

    private byte Shift(string name, byte a, byte result, bool carry)
    {
        SetFlags(result, carry, overflow: false);
        return Finish(name, a, 1, result);
    }

    private byte Finish(string name, int a, int b, byte result)
    {
        cpu.Record(MicroOp.Alu(name, a, b, result));
        cpu.ChargeAlu(name, wide: false);
        return result;
    }

    private ushort Finish(string name, int a, int b, ushort result)
    {
        cpu.Record(MicroOp.Alu(name, a, b, result));
        cpu.ChargeAlu(name, wide: true);
        return result;
    }

    private void SetFlags(byte result, bool carry, bool overflow) =>
        SetFlags(result == 0, carry, (result & 0x80) != 0, overflow);

    private void SetFlags16(ushort result, bool carry, bool overflow) =>
        SetFlags(result == 0, carry, (result & 0x8000) != 0, overflow);

    private void SetFlags(bool zero, bool carry, bool negative, bool overflow)
    {
        int flags = cpu.FlagRegister & (1 << (int)Flag.Interrupt);
        if (zero) flags |= 1 << (int)Flag.Zero;
        if (carry) flags |= 1 << (int)Flag.Carry;
        if (negative) flags |= 1 << (int)Flag.Negative;
        if (overflow) flags |= 1 << (int)Flag.Overflow;
        cpu.FlagRegister = (byte)flags;
    }
}
