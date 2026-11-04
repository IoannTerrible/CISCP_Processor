namespace CISCProcessorLibrary;

/// <summary>
/// Byte addressable main memory. The address bus is 16 bits wide, so there are 65536 cells shared by
/// code, data, the hardware stack (which grows downwards from <see cref="CISCProcessor.InitialStackPointer"/>)
/// and the interrupt vector table at the top of the address space. 16 bit values are little-endian.
/// </summary>
public sealed class Memory
{
    /// <summary>Number of addressable bytes.</summary>
    public const int Size = 65536;

    /// <summary>Size of one 256 byte page, the unit the memory views work in.</summary>
    public const int PageSize = 256;

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

    /// <summary>Returns the little-endian 16 bit value stored at <paramref name="address"/>.</summary>
    public ushort ReadWord(int address) => (ushort)(cells[Wrap(address)] | (cells[Wrap(address + 1)] << 8));

    /// <summary>Stores a little-endian 16 bit value at <paramref name="address"/>.</summary>
    public void WriteWord(int address, ushort value)
    {
        cells[Wrap(address)] = (byte)value;
        cells[Wrap(address + 1)] = (byte)(value >> 8);
    }

    /// <summary>Sets every cell to zero.</summary>
    public void Clear() => Array.Clear(cells);

    /// <summary>Copies <paramref name="image"/> into memory starting at <paramref name="origin"/>.</summary>
    public void Load(ReadOnlySpan<byte> image, int origin = 0)
    {
        if (origin < 0 || origin >= Size)
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Origin must be inside the address space.");
        if (origin + image.Length > Size)
            throw new ArgumentException($"An image of {image.Length} bytes does not fit at address 0x{origin:X4}.", nameof(image));

        image.CopyTo(cells.AsSpan(origin));
    }

    /// <summary>Returns a copy of the whole address space.</summary>
    public byte[] ToArray() => (byte[])cells.Clone();

    /// <summary>Returns a read-only view over the whole address space.</summary>
    public ReadOnlySpan<byte> AsSpan() => cells;

    /// <summary>Returns a read-only view over one 256 byte page.</summary>
    public ReadOnlySpan<byte> Page(int page) => cells.AsSpan((page & 0xFF) * PageSize, PageSize);

    /// <summary>True when any byte of the page is non-zero.</summary>
    public bool IsPageUsed(int page)
    {
        foreach (byte b in Page(page))
            if (b != 0) return true;
        return false;
    }

    /// <summary>Wraps an address into the 16 bit address space, like a real address bus would.</summary>
    public static int Wrap(int address) => address & (Size - 1);
}
