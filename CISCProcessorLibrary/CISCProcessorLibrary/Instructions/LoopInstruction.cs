namespace CISCProcessorLibrary.Instructions;

/// <summary>
/// LOOP Rn, addr - decrements Rn and jumps when it is not zero yet. One CISC instruction replaces
/// the DEC / CMP / JNZ sequence a RISC machine would need. Flags are not affected.
/// </summary>
public sealed class LoopInstruction : Instruction
{
    public LoopInstruction(byte opcode)
        : base(opcode, "LOOP Rn, addr", 3, "Decrement Rn and jump to addr while Rn is not zero")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte counter = (byte)(ReadOperand(cpu, Slot(0), operands) - 1);
        WriteOperand(cpu, Slot(0), operands, counter);
        byte target = ReadOperand(cpu, Slot(1), operands);
        cpu.Jump(Mnemonic, target, counter != 0);
    }
}
