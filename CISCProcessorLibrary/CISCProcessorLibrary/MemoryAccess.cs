namespace CISCProcessorLibrary;

/// <summary>Why the processor touched memory. Reported in <see cref="MicroOp.Name"/> of read/write micro-ops.</summary>
public static class MemoryAccess
{
    public const string Opcode = "opcode";
    public const string Operand = "operand";
    public const string Data = "data";
    public const string Stack = "stack";
}
