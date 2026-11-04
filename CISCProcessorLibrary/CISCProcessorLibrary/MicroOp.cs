namespace CISCProcessorLibrary;

/// <summary>Kinds of micro-operations the control unit reports while executing one instruction.</summary>
public enum MicroOpKind
{
    /// <summary>Opcode byte read from memory (address bus = IP).</summary>
    Fetch,

    /// <summary>A byte was read from memory (operand, data or stack).</summary>
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

    /// <summary>Register name, ALU operation, access purpose, fault kind or jump condition.</summary>
    public string? Name { get; init; }

    /// <summary>Memory address, port number or jump target. -1 when not applicable.</summary>
    public int Address { get; init; } = -1;

    /// <summary>Byte transferred on the data bus, ALU result, output value or 1/0 for a taken jump.</summary>
    public int Value { get; init; }

    /// <summary>Register / flags value before the change, or ALU operand A.</summary>
    public int Before { get; init; }

    /// <summary>Register / flags value after the change, or ALU operand B.</summary>
    public int After { get; init; }

    /// <summary>Human readable text (disassembly or fault message).</summary>
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

    public static MicroOp Halt() =>
        new() { Kind = MicroOpKind.Halt, Name = "HLT" };

    public static MicroOp Fault(FaultKind fault, string message) =>
        new() { Kind = MicroOpKind.Fault, Name = fault.ToString(), Text = message };

    public override string ToString() => Kind switch
    {
        MicroOpKind.Fetch => $"FETCH  [{Address:X2}] -> IR = {Value:X2}",
        MicroOpKind.Read => $"READ   [{Address:X2}] -> {Value:X2} ({Name})",
        MicroOpKind.Write => $"WRITE  [{Address:X2}] <- {Value:X2} ({Name})",
        MicroOpKind.Decode => $"DECODE {Value:X2} = {Text}",
        MicroOpKind.Execute => $"EXEC   {Name}",
        MicroOpKind.Alu => $"ALU    {Name} {Before:X2}, {After:X2} -> {Value:X2}",
        MicroOpKind.Register => $"REG    {Name} {Before:X2} -> {After:X2}",
        MicroOpKind.Flags => $"FLAGS  {ProcessorState.FormatFlags((byte)Before)} -> {Text}",
        MicroOpKind.Jump => $"JUMP   {Name} -> {Address:X2} ({(Value != 0 ? "taken" : "not taken")})",
        MicroOpKind.Output => $"OUT    port {Address} <- {Value}",
        MicroOpKind.Halt => "HALT",
        MicroOpKind.Fault => $"FAULT  {Name}: {Text}",
        _ => Kind.ToString(),
    };
}
