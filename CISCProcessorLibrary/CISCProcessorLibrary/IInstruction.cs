namespace CISCProcessorLibrary;

/// <summary>A machine instruction of the CISC-8 instruction set.</summary>
public interface IInstruction
{
    /// <summary>The byte that encodes the instruction in memory.</summary>
    byte Opcode { get; }

    /// <summary>Assembly syntax template, for example "ADD A, [addr]".</summary>
    InstructionSyntax Syntax { get; }

    /// <summary>Upper-case mnemonic, for example "ADD".</summary>
    string Mnemonic { get; }

    /// <summary>Base number of clock cycles the instruction takes (some instructions add cycles at run time).</summary>
    int Cycles { get; }

    /// <summary>Total size in bytes including the opcode.</summary>
    int Length { get; }

    /// <summary>One line description used by the instruction set reference.</summary>
    string Description { get; }

    /// <summary>Performs the instruction on <paramref name="cpu"/> using the operand bytes that followed the opcode.</summary>
    void Execute(CISCProcessor cpu, Operands operands);
}
