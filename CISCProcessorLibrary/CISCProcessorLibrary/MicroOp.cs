namespace CISCProcessorLibrary;

/// <summary>Kinds of micro-operations the control unit reports while executing one instruction.</summary>
public enum MicroOpKind
{
    /// <summary>Opcode byte read from memory (address bus = IP).</summary>
    Fetch,

    /// <summary>A byte was read from memory (operand, data, stack or vector).</summary>
    Read,

    /// <summary>A byte was written to memory (data or stack).</summary>
    Write,

    /// <summary>The opcode was recognised; <see cref="MicroOp.Text"/> holds the disassembled instruction.</summary>
    Decode,

    /// <summary>Execution of the decoded instruction started.</summary>
    Execute,

    /// <summary>The ALU computed a result (<see cref="MicroOp.Before"/> = operand A, <see cref="MicroOp.After"/> = operand B).</summary>
    Alu,

    /// <summary>A register changed value.</summary>
    Register,

    /// <summary>The FLAGS register changed value.</summary>
    Flags,

    /// <summary>A conditional or unconditional transfer of control was evaluated.</summary>
    Jump,

    /// <summary>A byte was written to an output port.</summary>
    Output,

    /// <summary>A byte was read from an input port.</summary>
    Input,

    /// <summary>An interrupt request was acknowledged (<see cref="MicroOp.Value"/> = IRQ number, <see cref="MicroOp.Address"/> = handler).</summary>
    Interrupt,

    /// <summary>The processor idled for some cycles waiting for an interrupt.</summary>
    Wait,

    /// <summary>The timing model reported something about the micro-architecture (cache, prefetch queue, branch predictor).</summary>
    Timing,

    /// <summary>The processor halted normally.</summary>
    Halt,

    /// <summary>The processor halted because of a fault.</summary>
    Fault,
}

/// <summary>
/// One elementary action performed by the processor while executing an instruction.
/// The record is intentionally flat so it serialises to JSON without any custom converters;
/// which properties are meaningful depends on <see cref="Kind"/> (see the factory methods).
/// </summary>
public sealed record MicroOp
{
    /// <summary>What happened.</summary>
    public MicroOpKind Kind { get; init; }

    /// <summary>Register name, ALU operation, access purpose, fault kind, jump condition or timing event name.</summary>
    public string? Name { get; init; }

    /// <summary>Memory address, port number or jump target. -1 when not applicable.</summary>
    public int Address { get; init; } = -1;

    /// <summary>Byte transferred on the data bus, ALU result, output value, cycles, or 1/0 for a taken jump.</summary>
    public int Value { get; init; }

    /// <summary>Register / flags value before the change, or ALU operand A.</summary>
    public int Before { get; init; }

    /// <summary>Register / flags value after the change, or ALU operand B.</summary>
    public int After { get; init; }

    /// <summary>Human readable text (disassembly, timing event or fault message).</summary>
    public string? Text { get; init; }

    public static MicroOp Fetch(int address, byte opcode) =>
        new() { Kind = MicroOpKind.Fetch, Address = address, Value = opcode, Name = "opcode" };

    public static MicroOp Read(int address, byte value, string purpose) =>
        new() { Kind = MicroOpKind.Read, Address = address, Value = value, Name = purpose };

    public static MicroOp Write(int address, byte value, string purpose) =>
        new() { Kind = MicroOpKind.Write, Address = address, Value = value, Name = purpose };

    public static MicroOp Decode(byte opcode, string mnemonic, string disassembly) =>
        new() { Kind = MicroOpKind.Decode, Value = opcode, Name = mnemonic, Text = disassembly };

    public static MicroOp Execute(string mnemonic) =>
        new() { Kind = MicroOpKind.Execute, Name = mnemonic };

    public static MicroOp Alu(string operation, int a, int b, int result) =>
        new() { Kind = MicroOpKind.Alu, Name = operation, Before = a, After = b, Value = result };

    public static MicroOp Register(string name, int before, int after) =>
        new() { Kind = MicroOpKind.Register, Name = name, Before = before, After = after, Value = after };

    public static MicroOp Flags(byte before, byte after) =>
        new() { Kind = MicroOpKind.Flags, Name = "FLAGS", Before = before, After = after, Value = after, Text = ProcessorState.FormatFlags(after) };

    public static MicroOp Jump(string condition, int target, bool taken) =>
        new() { Kind = MicroOpKind.Jump, Name = condition, Address = target, Value = taken ? 1 : 0 };

    public static MicroOp Output(int port, byte value) =>
        new() { Kind = MicroOpKind.Output, Address = port, Value = value, Name = "port" };

    public static MicroOp Input(int port, byte value) =>
        new() { Kind = MicroOpKind.Input, Address = port, Value = value, Name = "port" };

    public static MicroOp Interrupt(int irq, int handler) =>
        new() { Kind = MicroOpKind.Interrupt, Name = $"IRQ{irq}", Value = irq, Address = handler, Text = $"IRQ{irq} -> 0x{handler:X4}" };

    public static MicroOp Wait(int cycles) =>
        new() { Kind = MicroOpKind.Wait, Name = "wait", Value = cycles, Text = "waiting for an interrupt" };

    public static MicroOp Timing(string name, string text, int cycles, int address = -1) =>
        new() { Kind = MicroOpKind.Timing, Name = name, Text = text, Value = cycles, Address = address };

    public static MicroOp Halt() =>
        new() { Kind = MicroOpKind.Halt, Name = "HLT" };

    public static MicroOp Fault(FaultKind fault, string message) =>
        new() { Kind = MicroOpKind.Fault, Name = fault.ToString(), Text = message };

    private static string Hex(int value) => value > 0xFF ? value.ToString("X4") : value.ToString("X2");

    public override string ToString() => Kind switch
    {
        MicroOpKind.Fetch => $"FETCH  [{Address:X4}] -> IR = {Value:X2}",
        MicroOpKind.Read => $"READ   [{Address:X4}] -> {Value:X2} ({Name})",
        MicroOpKind.Write => $"WRITE  [{Address:X4}] <- {Value:X2} ({Name})",
        MicroOpKind.Decode => $"DECODE {Value:X2} = {Text}",
        MicroOpKind.Execute => $"EXEC   {Name}",
        MicroOpKind.Alu => $"ALU    {Name} {Hex(Before)}, {Hex(After)} -> {Hex(Value)}",
        MicroOpKind.Register => $"REG    {Name} {Hex(Before)} -> {Hex(After)}",
        MicroOpKind.Flags => $"FLAGS  {ProcessorState.FormatFlags((byte)Before)} -> {Text}",
        MicroOpKind.Jump => $"JUMP   {Name} -> {Address:X4} ({(Value != 0 ? "taken" : "not taken")})",
        MicroOpKind.Output => $"OUT    port {Address} <- {Value}",
        MicroOpKind.Input => $"IN     port {Address} -> {Value}",
        MicroOpKind.Interrupt => $"INTR   {Text}",
        MicroOpKind.Wait => $"WAIT   {Value} cycles",
        MicroOpKind.Timing => $"TIMING {Name}: {Text} (+{Value})",
        MicroOpKind.Halt => "HALT",
        MicroOpKind.Fault => $"FAULT  {Name}: {Text}",
        _ => Kind.ToString(),
    };
}
