namespace CISCProcessorLibrary.Instructions;

/// <summary>Condition a jump tests before transferring control.</summary>
public enum JumpCondition
{
    Always,
    Zero,
    NotZero,
    Carry,
    NotCarry,
    Sign,
    NotSign,
    Overflow,
    NotOverflow,
}

/// <summary>JMP / JZ / JNZ / JC / JNC / JS / JNS / JO / JNO addr - conditional transfer of control. A taken jump costs one extra cycle.</summary>
public sealed class JumpInstruction : Instruction
{
    public JumpInstruction(byte opcode, string syntax, int cycles, JumpCondition condition, string description)
        : base(opcode, syntax, cycles, description)
    {
        Condition = condition;
    }

    public JumpCondition Condition { get; }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte target = ReadOperand(cpu, Slot(0), operands);
        cpu.Jump(Mnemonic, target, IsSatisfied(cpu));
    }

    private bool IsSatisfied(CISCProcessor cpu) => Condition switch
    {
        JumpCondition.Always => true,
        JumpCondition.Zero => cpu.IsFlagSet(Flag.Zero),
        JumpCondition.NotZero => !cpu.IsFlagSet(Flag.Zero),
        JumpCondition.Carry => cpu.IsFlagSet(Flag.Carry),
        JumpCondition.NotCarry => !cpu.IsFlagSet(Flag.Carry),
        JumpCondition.Sign => cpu.IsFlagSet(Flag.Negative),
        JumpCondition.NotSign => !cpu.IsFlagSet(Flag.Negative),
        JumpCondition.Overflow => cpu.IsFlagSet(Flag.Overflow),
        JumpCondition.NotOverflow => !cpu.IsFlagSet(Flag.Overflow),
        _ => false,
    };
}
