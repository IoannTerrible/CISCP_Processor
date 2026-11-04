namespace CISCProcessorLibrary.Timing;

/// <summary>
/// The three micro-architectures the simulator can model. The instruction set and the results of a
/// program are identical in all of them; only the number of clock cycles, and the events the timing
/// model reports, change. Each one mirrors a real generation of CISC processors.
/// </summary>
public enum Microarchitecture
{
    /// <summary>1975: a multi-cycle control unit like the Intel 8080 / Zilog Z80. Every bus cycle costs T-states, nothing overlaps.</summary>
    Classic1975,

    /// <summary>1985: an 8086 style bus interface unit with a 6 byte prefetch queue that fills while the execution unit is busy.</summary>
    Prefetch1985,

    /// <summary>1995: a Pentium style pipeline with separate instruction and data caches and a branch target buffer.</summary>
    Pipelined1995,
}

/// <summary>Everything the user interface needs to draw the state of the current timing model.</summary>
public sealed record TimingSnapshot(
    Microarchitecture Kind,
    string Name,
    IReadOnlyDictionary<string, long> Statistics,
    int PrefetchQueueAddress,
    int PrefetchQueueLength,
    int PrefetchQueueCapacity,
    IReadOnlyList<int> InstructionCacheTags,
    IReadOnlyList<int> DataCacheTags,
    int CacheLineSize,
    IReadOnlyList<int> BranchPredictorCounters);
