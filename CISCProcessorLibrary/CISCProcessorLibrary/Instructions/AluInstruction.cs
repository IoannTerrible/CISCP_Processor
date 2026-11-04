namespace CISCProcessorLibrary.Instructions;

/// <summary>
/// ADD / ADC / SUB / SBB / CMP / AND / OR / XOR - combines the accumulator with a second operand
/// through the ALU. CMP only updates the flags.
/// </summary>
public sealed class AluInstruction : Instruction
{
    public AluInstruction(byte opcode, string syntax, int cycles, AluOperation operation, string description)
        : base(opcode, syntax, cycles, description)
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
