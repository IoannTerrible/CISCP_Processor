namespace CISCProcessorLibrary.Instructions;

/// <summary>
/// MOV destination, source - copies a byte between registers, memory and immediates.
/// Which addressing modes are used comes entirely from the syntax template, so one class
/// covers every MOV form of the instruction set. Flags are not affected.
/// </summary>
public sealed class MoveInstruction : Instruction
{
    public MoveInstruction(byte opcode, string syntax, int cycles, string description)
        : base(opcode, syntax, cycles, description)
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte value = ReadOperand(cpu, Slot(1), operands);
        WriteOperand(cpu, Slot(0), operands, value);
    }
}
