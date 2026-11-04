namespace CISCProcessorLibrary.Timing;

/// <summary>
/// 1975: a multi-cycle control unit in the spirit of the Intel 8080. Each machine cycle is a bus
/// cycle: the opcode fetch (M1) takes 4 T-states, every further memory byte 3 T-states, and only a few
/// operations need extra internal cycles. Nothing overlaps, so the cycle count of an instruction is
/// simply the sum of its bus cycles - MOV A, [addr] costs 4 + 3 + 3 + 3 = 13 T-states, like LDA on the 8080.
/// </summary>
public sealed class ClassicTiming : TimingModel
{
    public const int OpcodeFetchCycles = 4;
    public const int BusCycles = 3;
    public const int InternalCycle = 1;
    public const int MultiplyCycles = 16;
    public const int DivideCycles = 24;
    public const int InterruptCycles = 11;
    public const int IdleCycles = 4;

    public ClassicTiming(CISCProcessor cpu)
        : base(cpu)
    {
    }

    public override Microarchitecture Kind => Microarchitecture.Classic1975;

    public override string Name => "1975 multi-cycle (8080 style)";

    public override string Description =>
        "Every memory access is a separate machine cycle: 4 T-states for the opcode fetch, 3 for each other byte. " +
        "The control unit does one thing at a time, so a program's cycle count is the sum of its bus cycles.";

    public override TimingSnapshot Snapshot() =>
        new(Kind, Name, Statistics, -1, 0, 0, Array.Empty<int>(), Array.Empty<int>(), 0, Array.Empty<int>());

    public override void OnOpcodeFetch(int address)
    {
        Count("bus cycles");
        Charge(OpcodeFetchCycles);
    }

    public override void OnOperandFetch(int address)
    {
        Count("bus cycles");
        Charge(BusCycles);
    }

    public override void OnDataRead(int address, string purpose)
    {
        Count("bus cycles");
        Charge(BusCycles);
    }

    public override void OnDataWrite(int address, string purpose)
    {
        Count("bus cycles");
        Charge(BusCycles);
    }

    public override void OnPortAccess(int port, bool input)
    {
        Count("bus cycles");
        Charge(BusCycles);
    }

    public override void OnEffectiveAddress(OperandKind kind)
    {
        if (kind is OperandKind.Indexed or OperandKind.AddressRegisterDisplaced)
            Charge(2 * InternalCycle);
    }

    public override void OnAlu(string operation, bool wide)
    {
        Count("ALU operations");
        switch (operation)
        {
            case "MUL": Charge(MultiplyCycles); break;
            case "DIV": Charge(DivideCycles); break;
            case "INC" or "DEC" or "NEG" or "NOT" or "SHL" or "SHR" or "ROL" or "ROR": Charge(InternalCycle); break;
            case "ADD16" or "SUB16" or "CMP16": Charge(2 * InternalCycle); break;
            case "INC16" or "DEC16": Charge(InternalCycle); break;
            default: break; // 8 bit ADD / SUB / logic overlap with the next fetch, like on the 8080
        }
    }

    public override void OnJump(int from, int target, bool taken, bool conditional, string kind)
    {
        Count(taken ? "branches taken" : "branches not taken");
        if (taken && kind is "call") Charge(InternalCycle);
    }

    public override void OnInstructionComplete(IInstruction instruction)
    {
    }

    public override void OnInterrupt()
    {
        Count("interrupts");
        Charge(InterruptCycles, "interrupt", "interrupt acknowledge cycle");
    }

    public override int OnIdle()
    {
        Charge(IdleCycles);
        return IdleCycles;
    }
}
