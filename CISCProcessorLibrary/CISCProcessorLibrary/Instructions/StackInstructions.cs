namespace CISCProcessorLibrary.Instructions;

/// <summary>PUSH A / Rn / FLAGS / XR - stores the operand on the hardware stack (address registers as a word).</summary>
public sealed class PushInstruction : Instruction
{
    public PushInstruction(byte opcode, string syntax, string description)
        : base(opcode, syntax, description)
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        OperandSlot slot = Slot(0);
        if (slot.Kind.IsWide())
            cpu.PushWord(ReadOperand16(cpu, slot, operands));
        else
            cpu.Push(ReadOperand(cpu, slot, operands));
    }
}

/// <summary>POP A / Rn / FLAGS / XR - loads the operand from the hardware stack.</summary>
public sealed class PopInstruction : Instruction
{
    public PopInstruction(byte opcode, string syntax, string description)
        : base(opcode, syntax, description)
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        OperandSlot slot = Slot(0);
        if (slot.Kind.IsWide())
            WriteOperand16(cpu, slot, operands, cpu.PopWord());
        else
            WriteOperand(cpu, slot, operands, cpu.Pop());
    }
}

/// <summary>ENTER imm8 - builds a stack frame: pushes BP, points BP at it and reserves imm8 bytes of locals (80186 style).</summary>
public sealed class EnterInstruction : Instruction
{
    public EnterInstruction(byte opcode)
        : base(opcode, "ENTER imm8", "Push BP, BP = SP, SP = SP - imm8 (create a stack frame with imm8 bytes of locals)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte locals = ReadOperand(cpu, Slot(0), operands);
        cpu.PushWord(cpu.BasePointer);
        cpu.BasePointer = cpu.StackPointer;
        cpu.AdjustStackPointer(-locals);
    }
}

/// <summary>LEAVE - tears the stack frame down: SP = BP, pop BP.</summary>
public sealed class LeaveInstruction : Instruction
{
    public LeaveInstruction(byte opcode)
        : base(opcode, "LEAVE", "SP = BP, pop BP (destroy the stack frame built by ENTER)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        cpu.AdjustStackPointer(cpu.BasePointer - cpu.StackPointer);
        cpu.BasePointer = cpu.PopWord();
    }
}
