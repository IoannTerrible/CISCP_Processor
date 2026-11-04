namespace CISCProcessorLibrary.Timing;

/// <summary>
/// Decides how many clock cycles each micro-operation costs and reports micro-architectural events
/// (prefetch queue hits, cache misses, branch predictions) into the trace. The control unit calls the
/// hooks as it goes; the model charges cycles through <see cref="CISCProcessor.AddCycles"/>.
/// </summary>
public abstract class TimingModel
{
    private readonly Dictionary<string, long> statistics = new();

    protected TimingModel(CISCProcessor cpu)
    {
        Cpu = cpu;
    }

    /// <summary>Which generation this model represents.</summary>
    public abstract Microarchitecture Kind { get; }

    /// <summary>Short name shown in the user interfaces.</summary>
    public abstract string Name { get; }

    /// <summary>One paragraph about the modelled machine.</summary>
    public abstract string Description { get; }

    /// <summary>Counters such as cache hits and misses.</summary>
    public IReadOnlyDictionary<string, long> Statistics => statistics;

    protected CISCProcessor Cpu { get; }

    /// <summary>Creates the model for a micro-architecture.</summary>
    public static TimingModel Create(Microarchitecture kind, CISCProcessor cpu) => kind switch
    {
        Microarchitecture.Classic1975 => new ClassicTiming(cpu),
        Microarchitecture.Prefetch1985 => new PrefetchTiming(cpu),
        Microarchitecture.Pipelined1995 => new PipelinedTiming(cpu),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Returns the model to its power-on state (empty queues, cold caches).</summary>
    public virtual void Reset() => statistics.Clear();

    /// <summary>Current state for the user interfaces.</summary>
    public abstract TimingSnapshot Snapshot();

    /// <summary>The control unit is about to read the opcode at <paramref name="address"/>.</summary>
    public abstract void OnOpcodeFetch(int address);

    /// <summary>The control unit is about to read an operand byte at <paramref name="address"/>.</summary>
    public abstract void OnOperandFetch(int address);

    /// <summary>A data, stack or vector byte is read from <paramref name="address"/>.</summary>
    public abstract void OnDataRead(int address, string purpose);

    /// <summary>A data or stack byte is written to <paramref name="address"/>.</summary>
    public abstract void OnDataWrite(int address, string purpose);

    /// <summary>A byte was transferred over the I/O bus (IN or OUT).</summary>
    public abstract void OnPortAccess(int port, bool input);

    /// <summary>An effective address was computed for a memory operand.</summary>
    public abstract void OnEffectiveAddress(OperandKind kind);

    /// <summary>The ALU performed <paramref name="operation"/>.</summary>
    public abstract void OnAlu(string operation, bool wide);

    /// <summary>A transfer of control was evaluated.</summary>
    public abstract void OnJump(int from, int target, bool taken, bool conditional, string kind);

    /// <summary>An instruction finished executing.</summary>
    public abstract void OnInstructionComplete(IInstruction instruction);

    /// <summary>An interrupt request was acknowledged.</summary>
    public abstract void OnInterrupt();

    /// <summary>The processor idles for one quantum while waiting for an interrupt; returns the cycles spent.</summary>
    public abstract int OnIdle();

    /// <summary>Charges cycles to the current step and records a timing event when <paramref name="eventName"/> is given.</summary>
    protected void Charge(int cycles, string? eventName = null, string? text = null, int address = -1)
    {
        if (cycles > 0) Cpu.AddCycles(cycles);
        if (eventName is not null) Cpu.Record(MicroOp.Timing(eventName, text ?? eventName, cycles, address));
    }

    protected void Count(string statistic, long amount = 1)
    {
        statistics.TryGetValue(statistic, out long current);
        statistics[statistic] = current + amount;
    }

    protected long Get(string statistic) => statistics.TryGetValue(statistic, out long value) ? value : 0;
}
