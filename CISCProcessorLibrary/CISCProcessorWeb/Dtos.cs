using CISCProcessorLibrary;
using CISCProcessorLibrary.Timing;

namespace CISCProcessorWeb;

/// <summary>
/// JSON shapes exchanged with the visualizer. They match the objects the browser engine (web/js/cpu.js)
/// produces, so the page can animate traces from either engine without knowing which one it talks to.
/// </summary>
public static class Dtos
{
    public sealed record Info(string Engine, string Version, string Runtime, int Instructions, MicroarchitectureDto[] Microarchitectures);

    public sealed record MicroarchitectureDto(string Id, int Year, string Name, string Description);

    public sealed record SessionRequest(string? Source, string? Microarchitecture, string? Input);

    public sealed record SourceRequest(string? Source);

    public sealed record RunRequest(int? MaxSteps, int[]? StopAt);

    public sealed record InputRequest(string? Text);

    public sealed record AssemblyError(int Line, string Message, string Detail);

    public sealed record ListingLineDto(int LineNumber, int Address, int[] Bytes, string Source, bool IsInstruction, bool IsData);

    public sealed record ProgramDto(ListingLineDto[] Listing, Dictionary<string, int> Symbols, int EntryPoint, int Length);

    public sealed record StateDto(
        int A, int[] R, int Ip, int Sp, int Bp, int Si, int Di, int Flags, int Ir, int Mar, int Mdr, int Pending, string Phase,
        long Cycles, long Instructions, bool Halted, bool Waiting, string? Fault, string? FaultMessage, string FlagsText,
        int TimerPeriod, bool TimerEnabled, long TimerTicks, int InputQueued);

    public sealed record MicroOpDto(string Kind, string? Name, int Address, int Value, int Before, int After, string? Text);

    public sealed record TimingDto(
        string Kind, string Name, Dictionary<string, long> Statistics,
        int PrefetchQueueAddress, int PrefetchQueueLength, int PrefetchQueueCapacity,
        int[] InstructionCacheTags, int[] DataCacheTags, int CacheLineSize, int[] BranchPredictorCounters);

    public sealed record TraceDto(string Kind, int Address, int[] Bytes, string Disassembly, int Cycles, MicroOpDto[] Ops, StateDto State, TimingDto Timing);

    public sealed record SessionDto(string Id, ProgramDto Program, StateDto State, TimingDto Timing, string Microarchitecture, string Input);

    public sealed record InstructionDto(string Opcode, string Syntax, string Mnemonic, int Length, string Description);

    public static MicroarchitectureDto ToDto(Microarchitecture kind)
    {
        var probe = new CISCProcessor(kind);
        int year = kind switch { Microarchitecture.Classic1975 => 1975, Microarchitecture.Prefetch1985 => 1985, _ => 1995 };
        return new MicroarchitectureDto(kind.ToString(), year, probe.Timing.Name, probe.Timing.Description);
    }

    public static ProgramDto ToDto(AssembledProgram program) => new(
        program.Listing.Select(l => new ListingLineDto(
            l.LineNumber, l.Address, l.Bytes.Select(b => (int)b).ToArray(), l.Source, l.IsInstruction, l.IsData)).ToArray(),
        program.Symbols.ToDictionary(p => p.Key, p => p.Value),
        program.EntryPoint,
        program.Length);

    public static StateDto ToDto(CISCProcessor cpu)
    {
        ProcessorState s = cpu.GetState();
        return new StateDto(
            s.Accumulator, s.Registers.Select(r => (int)r).ToArray(), s.InstructionPointer, s.StackPointer, s.BasePointer,
            s.SourceIndex, s.DestinationIndex, s.FlagRegister, s.InstructionRegister, s.MemoryAddressRegister, s.MemoryDataRegister,
            s.PendingInterrupts, s.Phase.ToString().ToLowerInvariant(), s.Cycles, s.InstructionsExecuted, s.Halted, s.Waiting,
            s.Fault == FaultKind.None ? null : s.Fault.ToString(), cpu.FaultMessage, s.FlagsText,
            cpu.Timer.Period, cpu.Timer.Enabled, cpu.Timer.Ticks, cpu.InputDevice.Available);
    }

    public static MicroOpDto ToDto(MicroOp op) =>
        new(op.Kind.ToString().ToLowerInvariant(), op.Name, op.Address, op.Value, op.Before, op.After, op.Text);

    public static TimingDto ToDto(TimingSnapshot t) => new(
        t.Kind.ToString(), t.Name, t.Statistics.ToDictionary(p => p.Key, p => p.Value),
        t.PrefetchQueueAddress, t.PrefetchQueueLength, t.PrefetchQueueCapacity,
        t.InstructionCacheTags.ToArray(), t.DataCacheTags.ToArray(), t.CacheLineSize, t.BranchPredictorCounters.ToArray());

    public static TraceDto ToDto(StepTrace trace, CISCProcessor cpu) => new(
        trace.Kind.ToString().ToLowerInvariant(), trace.Address, trace.Bytes.Select(b => (int)b).ToArray(), trace.Disassembly, trace.Cycles,
        trace.MicroOps.Select(ToDto).ToArray(), ToDto(cpu), ToDto(cpu.Timing.Snapshot()));

    public static SessionDto ToDto(MachineSession session) => new(
        session.Id, ToDto(session.Program), ToDto(session.Cpu), ToDto(session.Cpu.Timing.Snapshot()),
        session.Cpu.Microarchitecture.ToString(), session.Cpu.InputDevice.PeekText());

    public static InstructionDto ToDto(IInstruction i) =>
        new($"{i.Opcode:X2}", i.Syntax.Template, i.Mnemonic, i.Length, i.Description);
}
