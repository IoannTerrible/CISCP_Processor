namespace CISCProcessorLibrary.Instructions;

/// <summary>The byte string primitives of the 8086.</summary>
public enum StringOperation
{
    /// <summary>LODSB: A = [SI], SI = SI + 1.</summary>
    Load,

    /// <summary>STOSB: [DI] = A, DI = DI + 1.</summary>
    Store,

    /// <summary>MOVSB: [DI] = [SI], SI and DI advance.</summary>
    Move,

    /// <summary>CMPSB: flags = [SI] - [DI], SI and DI advance.</summary>
    Compare,

    /// <summary>SCASB: flags = A - [DI], DI advances.</summary>
    Scan,
}

/// <summary>
/// LODSB / STOSB / MOVSB / CMPSB / SCASB - one string element per execution, with the index registers
/// advancing automatically. With the REP forms the processor repeats the primitive while R6 (the count
/// register) is not zero; REP CMPSB stops at the first difference and REP SCASB at the first match,
/// like REPE CMPSB and REPNE SCASB on the 8086.
/// </summary>
public sealed class StringInstruction : Instruction
{
    private const int SourceIndex = 0;
    private const int DestinationIndex = 1;

    /// <summary>The register REP uses as the element count (CX on the 8086).</summary>
    public const int CountRegister = 6;

    public StringInstruction(byte opcode, string syntax, StringOperation operation, bool repeat, string description)
        : base(opcode, syntax, description)
    {
        Operation = operation;
        Repeat = repeat;
    }

    public StringOperation Operation { get; }

    public bool Repeat { get; }

    public override void Execute(CISCProcessor cpu, Operands operands)
    {
        if (!Repeat)
        {
            Step(cpu);
            return;
        }

        while (cpu.ReadRegister(CountRegister) != 0)
        {
            Step(cpu);
            cpu.WriteRegister(CountRegister, (byte)(cpu.ReadRegister(CountRegister) - 1));
            if (Operation == StringOperation.Compare && !cpu.IsFlagSet(Flag.Zero)) break;   // REPE: stop at a difference
            if (Operation == StringOperation.Scan && cpu.IsFlagSet(Flag.Zero)) break;       // REPNE: stop at a match
        }
    }

    private void Step(CISCProcessor cpu)
    {
        ushort si = cpu.ReadAddressRegister(SourceIndex);
        ushort di = cpu.ReadAddressRegister(DestinationIndex);
        switch (Operation)
        {
            case StringOperation.Load:
                cpu.Accumulator = cpu.ReadMemory(si, MemoryAccess.Data);
                cpu.WriteAddressRegister(SourceIndex, (ushort)(si + 1));
                break;
            case StringOperation.Store:
                cpu.WriteMemory(di, cpu.Accumulator, MemoryAccess.Data);
                cpu.WriteAddressRegister(DestinationIndex, (ushort)(di + 1));
                break;
            case StringOperation.Move:
                cpu.WriteMemory(di, cpu.ReadMemory(si, MemoryAccess.Data), MemoryAccess.Data);
                cpu.WriteAddressRegister(SourceIndex, (ushort)(si + 1));
                cpu.WriteAddressRegister(DestinationIndex, (ushort)(di + 1));
                break;
            case StringOperation.Compare:
                cpu.Alu.Apply(AluOperation.Compare, cpu.ReadMemory(si, MemoryAccess.Data), cpu.ReadMemory(di, MemoryAccess.Data));
                cpu.WriteAddressRegister(SourceIndex, (ushort)(si + 1));
                cpu.WriteAddressRegister(DestinationIndex, (ushort)(di + 1));
                break;
            case StringOperation.Scan:
                cpu.Alu.Apply(AluOperation.Compare, cpu.Accumulator, cpu.ReadMemory(di, MemoryAccess.Data));
                cpu.WriteAddressRegister(DestinationIndex, (ushort)(di + 1));
                break;
        }
    }
}
