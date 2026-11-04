namespace CISCProcessorLibrary;

/// <summary>
/// Byte addressable main memory. The address space is 8 bits wide, so there are exactly 256 cells
/// shared by code, data and the hardware stack (which grows downwards from 0xFF).
/// </summary>
public sealed class Memory
{
    /// <summary>Number of addressable bytes.</summary>
    public const int Size = 256;

    private readonly byte[] cells = new byte[Size];

    /// <summary>Reads or writes a single byte. Addresses wrap around modulo <see cref="Size"/>.</summary>
    public byte this[int address]
    {
        get => cells[Wrap(address)];
        set => cells[Wrap(address)] = value;
    }

    /// <summary>Returns the byte stored at <paramref name="address"/>.</summary>
    public byte Read(int address) => cells[Wrap(address)];

    /// <summary>Stores <paramref name="value"/> at <paramref name="address"/>.</summary>
    public void Write(int address, byte value) => cells[Wrap(address)] = value;

    /// <summary>Sets every cell to zero.</summary>
    public void Clear() => Array.Clear(cells);

    /// <summary>Copies <paramref name="image"/> into memory starting at <paramref name="origin"/>.</summary>
    public void Load(ReadOnlySpan<byte> image, int origin = 0)
    {
        if (origin < 0 || origin >= Size)
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Origin must be inside the address space.");
        if (origin + image.Length > Size)
            throw new ArgumentException($"An image of {image.Length} bytes does not fit at address 0x{origin:X2}.", nameof(image));

        image.CopyTo(cells.AsSpan(origin));
    }

    /// <summary>Returns a copy of the whole address space.</summary>
    public byte[] ToArray() => (byte[])cells.Clone();

    /// <summary>Returns a read-only view over the whole address space.</summary>
    public ReadOnlySpan<byte> AsSpan() => cells;

    /// <summary>Wraps an address into the 8 bit address space, like a real address bus would.</summary>
    public static int Wrap(int address) => address & (Size - 1);
}
