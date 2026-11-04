namespace CISCProcessorLibrary.Instructions;

/// <summary>
/// ADD / ADC / SUB / SBB / CMP / AND / OR / XOR - combines the accumulator with a second operand
/// through the ALU. CMP only updates the flags.
/// </summary>
public sealed class AluInstruction : Instruction
{
    public AluInstruction(byte opcode, string syntax, AluOperation operation, string description)
        : base(opcode, syntax, description)
    {
        Operation = operation;
    }

    public AluOperation Operation { get; }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte a = cpu.Accumulator;
        byte b = ReadOperand(cpu, Slot(1), operands);
        byte result = cpu.Alu.Apply(Operation, a, b);
        if (Operation != AluOperation.Compare)
            cpu.Accumulator = result;
    }
}

/// <summary>
/// INC / DEC / NOT / NEG / SHL / SHR / ROL / ROR - reads the single operand, transforms it in the ALU
/// and writes it back to the same place (a read-modify-write when the operand lives in memory).
/// </summary>
public sealed class UnaryInstruction : Instruction
{
    public UnaryInstruction(byte opcode, string syntax, UnaryOperation operation, string description)
        : base(opcode, syntax, description)
    {
        Operation = operation;
    }

    public UnaryOperation Operation { get; }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        OperandSlot target = Slot(0);
        byte value = ReadOperand(cpu, target, operands);
        byte result = cpu.Alu.Apply(Operation, value);
        WriteOperand(cpu, target, operands, result);
    }
}

/// <summary>ADD / SUB / CMP XR, imm16 and INC / DEC XR - 16 bit arithmetic on the address registers.</summary>
public sealed class AddressArithmeticInstruction : Instruction
{
    public AddressArithmeticInstruction(byte opcode, string syntax, AddressOperation operation, string description)
        : base(opcode, syntax, description)
    {
        Operation = operation;
    }

    public AddressOperation Operation { get; }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        OperandSlot target = Slot(0);
        ushort a = ReadOperand16(cpu, target, operands);
        ushort b = Syntax.Operands.Count == 1 ? (ushort)0
            : Slot(1).Kind == OperandKind.Accumulator ? cpu.Accumulator
            : ReadOperand16(cpu, Slot(1), operands);
        ushort result = cpu.Alu.Apply(Operation, a, b);
        if (Operation != AddressOperation.Compare)
            WriteOperand16(cpu, target, operands, result);
    }
}

/// <summary>MUL Rn - R7:A = A * Rn, the 16 bit product of the 8086 (AH:AL) with R7 playing AH.</summary>
public sealed class MultiplyInstruction : Instruction
{
    public MultiplyInstruction(byte opcode)
        : base(opcode, "MUL Rn", "R7:A = A * Rn (16 bit product, R7 receives the high byte)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        ushort product = cpu.Alu.Multiply(cpu.Accumulator, ReadOperand(cpu, Slot(0), operands));
        cpu.Accumulator = (byte)product;
        cpu.WriteRegister(7, (byte)(product >> 8));
    }
}

/// <summary>DIV Rn - A = (R7:A) / Rn, R7 = remainder; faults on a zero divisor or an 8 bit overflow.</summary>
public sealed class DivideInstruction : Instruction
{
    public DivideInstruction(byte opcode)
        : base(opcode, "DIV Rn", "A = R7:A / Rn, R7 = remainder (unsigned); faults on zero or overflow")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        ushort dividend = (ushort)((cpu.ReadRegister(7) << 8) | cpu.Accumulator);
        (byte quotient, byte remainder) = cpu.Alu.Divide(dividend, ReadOperand(cpu, Slot(0), operands));
        cpu.Accumulator = quotient;
        cpu.WriteRegister(7, remainder);
    }
}
