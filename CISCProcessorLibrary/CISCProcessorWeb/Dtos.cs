using CISCProcessorLibrary;

namespace CISCProcessorWeb;

/// <summary>
/// JSON shapes exchanged with the visualizer. They match the objects the browser engine (web/js/cpu.js)
/// produces, so the page can animate traces from either engine without knowing which one it talks to.
/// </summary>
public static class Dtos
{
    public sealed record Info(string Engine, string Version, string Runtime, int Instructions);

    public sealed record SourceRequest(string? Source);

    public sealed record RunRequest(int? MaxInstructions, int[]? StopAt);

    public sealed record AssemblyError(int Line, string Message, string Detail);

    public sealed record ListingLineDto(int LineNumber, int Address, int[] Bytes, string Source, bool IsInstruction, bool IsData);

    public sealed record ProgramDto(ListingLineDto[] Listing, Dictionary<string, int> Symbols, int EntryPoint, int Length);

    public sealed record StateDto(
        int A, int[] R, int Ip, int Sp, int Flags, int Ir, int Mar, int Mdr, string Phase,
        long Cycles, long Instructions, bool Halted, string? Fault, string? FaultMessage, string FlagsText);

    public sealed record MicroOpDto(string Kind, string? Name, int Address, int Value, int Before, int After, string? Text);

    public sealed record TraceDto(int Address, int[] Bytes, string Disassembly, int Cycles, MicroOpDto[] Ops, StateDto State);

    public sealed record SessionDto(string Id, ProgramDto Program, StateDto State);

    public sealed record InstructionDto(string Opcode, string Syntax, string Mnemonic, int Length, int Cycles, string Description);

    public static ProgramDto ToDto(AssembledProgram program) => new(
        program.Listing.Select(l => new ListingLineDto(
            l.LineNumber, l.Address, l.Bytes.Select(b => (int)b).ToArray(), l.Source, l.IsInstruction, !l.IsInstruction && l.Bytes.Length > 0)).ToArray(),
        program.Symbols.ToDictionary(p => p.Key, p => p.Value),
        program.EntryPoint,
        program.Length);

    public static StateDto ToDto(ProcessorState s, string? faultMessage) => new(
        s.Accumulator, s.Registers.Select(r => (int)r).ToArray(), s.InstructionPointer, s.StackPointer, s.FlagRegister,
        s.InstructionRegister, s.MemoryAddressRegister, s.MemoryDataRegister, s.Phase.ToString().ToLowerInvariant(),
        s.Cycles, s.InstructionsExecuted, s.Halted, s.Fault == FaultKind.None ? null : s.Fault.ToString(), faultMessage, s.FlagsText);

    public static MicroOpDto ToDto(MicroOp op) =>
        new(op.Kind.ToString().ToLowerInvariant(), op.Name, op.Address, op.Value, op.Before, op.After, op.Text);

    public static TraceDto ToDto(StepTrace trace, string? faultMessage) => new(
        trace.Address, trace.Bytes.Select(b => (int)b).ToArray(), trace.Disassembly, trace.Cycles,
        trace.MicroOps.Select(ToDto).ToArray(), ToDto(trace.StateAfter, faultMessage));

    public static InstructionDto ToDto(IInstruction i) =>
        new($"{i.Opcode:X2}", i.Syntax.Template, i.Mnemonic, i.Length, i.Cycles, i.Description);
}
