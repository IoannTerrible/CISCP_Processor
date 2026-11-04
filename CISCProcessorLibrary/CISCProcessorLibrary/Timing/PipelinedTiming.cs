namespace CISCProcessorLibrary.Timing;

/// <summary>
/// 1995: a Pentium style core. Instructions flow through a five stage pipeline, so a simple instruction
/// completes every cycle. Separate instruction and data caches (16 lines of 16 bytes each, direct-mapped)
/// hide the slow memory; a cache miss stalls the pipeline for the memory latency. A branch target buffer
/// with 2 bit saturating counters predicts conditional jumps; a wrong prediction flushes the pipeline.
/// </summary>
public sealed class PipelinedTiming : TimingModel
{
    public const int Lines = 16;
    public const int LineSize = 16;
    public const int IssueCycles = 1;
    public const int MissCycles = 8;
    public const int PortCycles = 8;
    public const int MultiplyCycles = 10;
    public const int DivideCycles = 40;
    public const int MispredictCycles = 3;
    public const int InterruptCycles = 20;
    public const int PredictorEntries = 16;
    public const int IdleCycles = 1;

    private readonly int[] instructionTags = new int[Lines];
    private readonly int[] dataTags = new int[Lines];
    private readonly int[] predictor = new int[PredictorEntries];
    private readonly int[] targets = new int[PredictorEntries];
    private int lastFetchLine = -1;

    public PipelinedTiming(CISCProcessor cpu)
        : base(cpu)
    {
        Reset();
    }

    public override Microarchitecture Kind => Microarchitecture.Pipelined1995;

    public override string Name => "1995 pipeline + caches (Pentium style)";

    public override string Description =>
        "Fetch, decode, execute, memory and write-back overlap, so one simple instruction retires per cycle. " +
        "Instruction and data caches answer in one cycle; a miss costs the memory latency. " +
        "A branch target buffer guesses jumps and a wrong guess flushes the pipeline.";

    public IReadOnlyList<int> InstructionCacheTags => instructionTags;

    public IReadOnlyList<int> DataCacheTags => dataTags;

    public IReadOnlyList<int> PredictorCounters => predictor;

    public override void Reset()
    {
        base.Reset();
        Array.Fill(instructionTags, -1);
        Array.Fill(dataTags, -1);
        Array.Fill(predictor, 1);     // weakly not taken, the cold state of a 2 bit counter
        Array.Fill(targets, -1);
        lastFetchLine = -1;
    }

    public override TimingSnapshot Snapshot() =>
        new(Kind, Name, Statistics, -1, 0, 0, (int[])instructionTags.Clone(), (int[])dataTags.Clone(), LineSize, (int[])predictor.Clone());

    public override void OnOpcodeFetch(int address)
    {
        lastFetchLine = -1;
        Access(instructionTags, address, "I-cache", "instruction cache");
        lastFetchLine = address / LineSize;
    }

    public override void OnOperandFetch(int address)
    {
        // Operand bytes in the same line as the opcode arrive with it; a line crossing is another lookup.
        if (address / LineSize == lastFetchLine) return;
        Access(instructionTags, address, "I-cache", "instruction cache");
        lastFetchLine = address / LineSize;
    }

    public override void OnDataRead(int address, string purpose) => Access(dataTags, address, "D-cache", "data cache");

    public override void OnDataWrite(int address, string purpose) => Access(dataTags, address, "D-cache", "data cache");

    public override void OnPortAccess(int port, bool input)
    {
        Count("port transfers");
        Charge(PortCycles, "I/O", "uncached port transfer", port);
    }

    public override void OnEffectiveAddress(OperandKind kind)
    {
        // Address generation has its own pipeline stage: no extra cycles.
    }

    public override void OnAlu(string operation, bool wide)
    {
        Count("ALU operations");
        switch (operation)
        {
            case "MUL": Charge(MultiplyCycles - IssueCycles, "multi-cycle", "multiplier busy"); break;
            case "DIV": Charge(DivideCycles - IssueCycles, "multi-cycle", "divider busy"); break;
            default: break; // one pipeline stage
        }
    }

    public override void OnJump(int from, int target, bool taken, bool conditional, string kind)
    {
        int slot = (from >> 1) & (PredictorEntries - 1);
        bool predictedTaken;
        bool targetKnown = targets[slot] == target;
        if (conditional)
            predictedTaken = predictor[slot] >= 2;
        else
            predictedTaken = targetKnown;    // the BTB knows where this jump went last time

        bool correct = predictedTaken == taken && (!taken || targetKnown);
        if (taken)
        {
            if (predictor[slot] < 3) predictor[slot]++;
            targets[slot] = target;
        }
        else if (predictor[slot] > 0)
        {
            predictor[slot]--;
        }

        if (correct)
        {
            Count("branches predicted");
            Charge(0, "predicted", $"{kind} predicted {(taken ? "taken" : "not taken")}", target);
        }
        else
        {
            Count("branches mispredicted");
            Charge(MispredictCycles, "mispredicted", $"{kind} mispredicted, pipeline flushed", target);
        }
    }

    public override void OnInstructionComplete(IInstruction instruction)
    {
        Count("instructions issued");
        Charge(IssueCycles);
    }

    public override void OnInterrupt()
    {
        Count("interrupts");
        Charge(InterruptCycles, "interrupt", "interrupt: pipeline flushed");
    }

    public override int OnIdle()
    {
        Charge(IdleCycles);
        return IdleCycles;
    }

    private void Access(int[] tags, int address, string cache, string description)
    {
        int line = (address / LineSize) % Lines;
        int tag = address / (LineSize * Lines);
        if (tags[line] == tag)
        {
            Count(cache + " hits");
            Charge(0, cache + " hit", $"{description} hit, line {line}", address);
        }
        else
        {
            Count(cache + " misses");
            tags[line] = tag;
            Charge(MissCycles, cache + " miss", $"{description} miss, line {line} filled from memory", address);
        }
    }
}
