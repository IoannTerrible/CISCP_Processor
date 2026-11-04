namespace CISCProcessorLibrary.Instructions;

/// <summary>CALL addr - pushes the return address and jumps to a subroutine.</summary>
public sealed class CallInstruction : Instruction
{
    public CallInstruction(byte opcode)
        : base(opcode, "CALL addr", 5, "Push the return address and jump to a subroutine")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte target = ReadOperand(cpu, Slot(0), operands);
        cpu.Push(cpu.InstructionPointer);
        cpu.Jump(Mnemonic, target, taken: true);
    }
}
