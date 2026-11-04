namespace CISCProcessorLibrary.Instructions;

/// <summary>RET - pops the return address pushed by CALL and continues after the call.</summary>
public sealed class ReturnInstruction : Instruction
{
    public ReturnInstruction(byte opcode)
        : base(opcode, "RET", 4, "Return from a subroutine")
    {
    }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        byte target = cpu.Pop();
        cpu.Jump(Mnemonic, target, taken: true);
    }
}
