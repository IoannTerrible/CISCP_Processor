namespace CISCProcessorLibrary.Instructions;

/// <summary>
/// INC / DEC / NOT / NEG / SHL / SHR / ROL / ROR - reads the single operand, transforms it in the ALU
/// and writes it back to the same place (a read-modify-write when the operand lives in memory).
/// </summary>
public sealed class UnaryInstruction : Instruction
{
    public UnaryInstruction(byte opcode, string syntax, int cycles, UnaryOperation operation, string description)
        : base(opcode, syntax, cycles, description)
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
