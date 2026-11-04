namespace CISCProcessorLibrary.Instructions;

/// <summary>Which of the "expensive" multi-cycle arithmetic instructions to perform.</summary>
public enum MultiplyDivideOperation
{
    Multiply,
    Divide,
    Modulo,
}

/// <summary>MUL / DIV / MOD Rn - A = A op Rn. These are the classic CISC multi-cycle instructions.</summary>
public sealed class MultiplyDivideInstruction : Instruction
{
    public MultiplyDivideInstruction(byte opcode, string syntax, int cycles, MultiplyDivideOperation operation, string description)
        : base(opcode, syntax, cycles, description)
    {
        Operation = operation;
    }

    public MultiplyDivideOperation Operation { get; }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte a = cpu.Accumulator;
        byte b = ReadOperand(cpu, Slot(0), operands);
        cpu.Accumulator = Operation switch
        {
            MultiplyDivideOperation.Multiply => cpu.Alu.Multiply(a, b),
            MultiplyDivideOperation.Divide => cpu.Alu.Divide(a, b),
            MultiplyDivideOperation.Modulo => cpu.Alu.Modulo(a, b),
            _ => throw new InvalidOperationException($"Unknown operation {Operation}."),
        };
    }
}
