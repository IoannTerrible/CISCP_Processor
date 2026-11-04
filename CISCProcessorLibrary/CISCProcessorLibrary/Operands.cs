namespace CISCProcessorLibrary;

/// <summary>The operand bytes that followed an opcode in memory (at most three).</summary>
public readonly struct Operands
{
    public const int MaxBytes = 3;

    private readonly byte first;
    private readonly byte second;
    private readonly byte third;

    public Operands(int count, byte first = 0, byte second = 0, byte third = 0)
    {
        if (count is < 0 or > MaxBytes)
            throw new ArgumentOutOfRangeException(nameof(count), count, $"An instruction has at most {MaxBytes} operand bytes.");
        Count = count;
        this.first = first;
        this.second = second;
        this.third = third;
    }

    /// <summary>How many operand bytes the instruction has.</summary>
    public int Count { get; }

    /// <summary>Returns the operand byte at <paramref name="index"/>.</summary>
    public byte this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"The instruction has {Count} operand byte(s).");
            return index switch { 0 => first, 1 => second, _ => third };
        }
    }

    /// <summary>Returns the little-endian 16 bit value stored at operand bytes <paramref name="offset"/> and <paramref name="offset"/> + 1.</summary>
    public ushort Word(int offset) => (ushort)(this[offset] | (this[offset + 1] << 8));

    /// <summary>Operand bytes as an array (empty when there are none).</summary>
    public byte[] ToArray() => Count switch
    {
        0 => Array.Empty<byte>(),
        1 => new[] { first },
        2 => new[] { first, second },
        _ => new[] { first, second, third },
    };

    /// <summary>Builds operands from raw bytes.</summary>
    public static Operands FromBytes(ReadOnlySpan<byte> bytes) => bytes.Length switch
    {
        0 => None,
        1 => new Operands(1, bytes[0]),
        2 => new Operands(2, bytes[0], bytes[1]),
        3 => new Operands(3, bytes[0], bytes[1], bytes[2]),
        _ => throw new ArgumentException($"At most {MaxBytes} operand bytes are supported.", nameof(bytes)),
    };

    public static Operands None => new(0);
}
