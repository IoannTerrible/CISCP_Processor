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

/// <summary>JMP / JZ / JNZ / JC / JNC / JS / JNS / JO / JNO addr - conditional transfer of control.</summary>
public sealed class JumpInstruction : Instruction
{
    public JumpInstruction(byte opcode, string syntax, JumpCondition condition, string description)
        : base(opcode, syntax, description)
    {
        Condition = condition;
    }

    public JumpCondition Condition { get; }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        ushort target = ReadOperand16(cpu, Slot(0), operands);
        cpu.Jump(Mnemonic, target, IsSatisfied(cpu), conditional: Condition != JumpCondition.Always, kind: "jump");
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

/// <summary>JMP [XR] - jumps to the 16 bit address stored in memory at XR: the jump table instruction.</summary>
public sealed class IndirectJumpInstruction : Instruction
{
    public IndirectJumpInstruction(byte opcode)
        : base(opcode, "JMP [XR]", "Jump to the word stored at [XR] (jump tables, computed goto)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        ushort target = ReadOperand16(cpu, Slot(0), operands);
        cpu.Jump("JMP", target, taken: true, conditional: false, kind: "indirect");
    }
}

/// <summary>CALL addr - pushes the return address and jumps to a subroutine.</summary>
public sealed class CallInstruction : Instruction
{
    public CallInstruction(byte opcode)
        : base(opcode, "CALL addr", "Push the return address and jump to a subroutine")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        ushort target = ReadOperand16(cpu, Slot(0), operands);
        cpu.PushWord(cpu.InstructionPointer);
        cpu.Jump(Mnemonic, target, taken: true, conditional: false, kind: "call");
    }
}

/// <summary>RET - pops the return address pushed by CALL and continues after the call.</summary>
public sealed class ReturnInstruction : Instruction
{
    public ReturnInstruction(byte opcode)
        : base(opcode, "RET", "Return from a subroutine")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        ushort target = cpu.PopWord();
        cpu.Jump(Mnemonic, target, taken: true, conditional: false, kind: "ret");
    }
}

/// <summary>
/// LOOP Rn, addr - decrements Rn and jumps when it is not zero yet. One CISC instruction replaces
/// the DEC / CMP / JNZ sequence a RISC machine would need. Flags are not affected.
/// </summary>
public sealed class LoopInstruction : Instruction
{
    public LoopInstruction(byte opcode)
        : base(opcode, "LOOP Rn, addr", "Decrement Rn and jump to addr while Rn is not zero")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte counter = (byte)(ReadOperand(cpu, Slot(0), operands) - 1);
        WriteOperand(cpu, Slot(0), operands, counter);
        ushort target = ReadOperand16(cpu, Slot(1), operands);
        cpu.Jump(Mnemonic, target, counter != 0, conditional: true, kind: "loop");
    }
}
