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

/// <summary>
/// The 8 bit arithmetic logic unit. Every operation returns the 8 bit result and updates the
/// Z, C, N and V flags of the owning processor exactly like a classic 8 bit CPU would.
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

    /// <summary>a + b (+ C). C = carry out of bit 7, V = signed overflow.</summary>
    public byte Add(byte a, byte b, bool useCarry)
    {
        int carryIn = useCarry && cpu.IsFlagSet(Flag.Carry) ? 1 : 0;
        int full = a + b + carryIn;
        byte result = (byte)full;
        bool overflow = ((a ^ result) & (b ^ result) & 0x80) != 0;
        SetFlags(result, carry: full > 0xFF, overflow);
        cpu.Record(MicroOp.Alu(useCarry ? "ADC" : "ADD", a, b, result));
        return result;
    }

    /// <summary>a - b (- C). C = borrow (unsigned a &lt; b), V = signed overflow.</summary>
    public byte Subtract(byte a, byte b, bool useBorrow, string name = "SUB")
    {
        int borrowIn = useBorrow && cpu.IsFlagSet(Flag.Carry) ? 1 : 0;
        int full = a - b - borrowIn;
        byte result = (byte)full;
        bool overflow = ((a ^ b) & (a ^ result) & 0x80) != 0;
        SetFlags(result, carry: full < 0, overflow);
        cpu.Record(MicroOp.Alu(name, a, b, result));
        return result;
    }

    /// <summary>A * Rn. The low byte is the result, C = V = 1 when the product does not fit in 8 bits.</summary>
    public byte Multiply(byte a, byte b)
    {
        int product = a * b;
        byte result = (byte)product;
        SetFlags(result, carry: product > 0xFF, overflow: product > 0xFF);
        cpu.Record(MicroOp.Alu("MUL", a, b, result));
        return result;
    }

    /// <summary>Unsigned A / Rn. Faults on a zero divisor.</summary>
    public byte Divide(byte a, byte b)
    {
        if (b == 0)
            cpu.RaiseFault(FaultKind.DivideByZero, "DIV by zero");
        byte result = (byte)(a / b);
        SetFlags(result, carry: false, overflow: false);
        cpu.Record(MicroOp.Alu("DIV", a, b, result));
        return result;
    }

    /// <summary>Unsigned A mod Rn. Faults on a zero divisor.</summary>
    public byte Modulo(byte a, byte b)
    {
        if (b == 0)
            cpu.RaiseFault(FaultKind.DivideByZero, "MOD by zero");
        byte result = (byte)(a % b);
        SetFlags(result, carry: false, overflow: false);
        cpu.Record(MicroOp.Alu("MOD", a, b, result));
        return result;
    }

    private byte Increment(byte a)
    {
        byte result = (byte)(a + 1);
        SetFlags(result, carry: cpu.IsFlagSet(Flag.Carry), overflow: a == 0x7F);
        cpu.Record(MicroOp.Alu("INC", a, 1, result));
        return result;
    }

    private byte Decrement(byte a)
    {
        byte result = (byte)(a - 1);
        SetFlags(result, carry: cpu.IsFlagSet(Flag.Carry), overflow: a == 0x80);
        cpu.Record(MicroOp.Alu("DEC", a, 1, result));
        return result;
    }

    private byte Negate(byte a)
    {
        byte result = (byte)(-a);
        SetFlags(result, carry: a != 0, overflow: a == 0x80);
        cpu.Record(MicroOp.Alu("NEG", a, 0, result));
        return result;
    }

    private byte Logic(string name, byte a, byte b, byte result)
    {
        SetFlags(result, carry: false, overflow: false);
        cpu.Record(MicroOp.Alu(name, a, b, result));
        return result;
    }

    private byte Shift(string name, byte a, byte result, bool carry)
    {
        SetFlags(result, carry, overflow: false);
        cpu.Record(MicroOp.Alu(name, a, 1, result));
        return result;
    }

    private void SetFlags(byte result, bool carry, bool overflow)
    {
        int flags = 0;
        if (result == 0) flags |= 1 << (int)Flag.Zero;
        if (carry) flags |= 1 << (int)Flag.Carry;
        if ((result & 0x80) != 0) flags |= 1 << (int)Flag.Negative;
        if (overflow) flags |= 1 << (int)Flag.Overflow;
        cpu.FlagRegister = (byte)flags;
    }
}
