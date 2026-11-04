namespace CISCProcessorLibrary.Instructions;

/// <summary>NOP - does nothing.</summary>
public sealed class NopInstruction : Instruction
{
    public NopInstruction(byte opcode)
        : base(opcode, "NOP", "No operation")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
    }
}

/// <summary>HLT - stops the clock, or waits for an interrupt when interrupts are enabled.</summary>
public sealed class HaltInstruction : Instruction
{
    public HaltInstruction(byte opcode)
        : base(opcode, "HLT", "Halt; with interrupts enabled, wait for the next interrupt instead")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) => cpu.Halt();
}

/// <summary>EI / DI - enable or disable maskable interrupts.</summary>
public sealed class InterruptFlagInstruction : Instruction
{
    public InterruptFlagInstruction(byte opcode, string syntax, bool enable, string description)
        : base(opcode, syntax, description)
    {
        Enable = enable;
    }

    public bool Enable { get; }

    public override void Execute(CISCProcessor cpu, Operands operands) => cpu.SetFlag(Flag.Interrupt, Enable);
}

/// <summary>IRET - returns from an interrupt handler, restoring IP and FLAGS from the stack.</summary>
public sealed class InterruptReturnInstruction : Instruction
{
    public InterruptReturnInstruction(byte opcode)
        : base(opcode, "IRET", "Return from an interrupt handler (pop IP, pop FLAGS)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) => cpu.ReturnFromInterrupt();
}

/// <summary>INT n - a software interrupt: the operating system call of the 1980s.</summary>
public sealed class SoftwareInterruptInstruction : Instruction
{
    public SoftwareInterruptInstruction(byte opcode)
        : base(opcode, "INT imm8", "Software interrupt through vector imm8 (0..7)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) =>
        cpu.EnterInterrupt(ReadOperand(cpu, Slot(0), operands) & 7, software: true);
}

/// <summary>CLC / STC - clears or sets the carry flag directly.</summary>
public sealed class FlagInstruction : Instruction
{
    public FlagInstruction(byte opcode, string syntax, Flag flag, bool value, string description)
        : base(opcode, syntax, description)
    {
        Flag = flag;
        Value = value;
    }

    public Flag Flag { get; }

    public bool Value { get; }

    public override void Execute(CISCProcessor cpu, Operands operands) => cpu.SetFlag(Flag, Value);
}

/// <summary>OUT port, A - writes the accumulator to an output port.</summary>
public sealed class OutInstruction : Instruction
{
    public OutInstruction(byte opcode)
        : base(opcode, "OUT port, A", "Write A to an output port (0 = decimal, 1 = character, 2..4 = timer)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) =>
        cpu.Output(ReadOperand(cpu, Slot(0), operands), cpu.Accumulator);
}

/// <summary>IN A, port - reads a byte from an input port into the accumulator.</summary>
public sealed class InInstruction : Instruction
{
    public InInstruction(byte opcode)
        : base(opcode, "IN A, port", "Read an input port into A (0 / 1 = input queue, 5 = timer ticks, 6 = input status)")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands) =>
        cpu.Accumulator = cpu.Input(ReadOperand(cpu, Slot(1), operands));
}
