namespace CISCProcessorLibrary;

/// <summary>The operand bytes that followed an opcode in memory (at most two).</summary>
public readonly struct Operands
{
    public Operands(int count, byte first = 0, byte second = 0)
    {
        if (count is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(count), count, "An instruction has at most two operand bytes.");
        Count = count;
        First = first;
        Second = second;
    }

    /// <summary>How many operand bytes the instruction has.</summary>
    public int Count { get; }

    /// <summary>First operand byte (0 when absent).</summary>
    public byte First { get; }

    /// <summary>Second operand byte (0 when absent).</summary>
    public byte Second { get; }

    /// <summary>Returns the operand byte at <paramref name="index"/>.</summary>
    public byte this[int index] => index switch
    {
        0 when Count > 0 => First,
        1 when Count > 1 => Second,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, $"The instruction has {Count} operand byte(s)."),
    };

    /// <summary>Operand bytes as an array (empty when there are none).</summary>
    public byte[] ToArray() => Count switch
    {
        0 => Array.Empty<byte>(),
        1 => new[] { First },
        _ => new[] { First, Second },
    };

    public static Operands None => new(0);
}
