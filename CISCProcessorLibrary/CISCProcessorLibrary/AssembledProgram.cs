using System.Text;

namespace CISCProcessorLibrary;

/// <summary>One source line of an assembled program together with the bytes it produced.</summary>
public sealed record ListingLine(int LineNumber, int Address, byte[] Bytes, string Source, bool IsInstruction)
{
    /// <summary>True for .db / .dw / .ds lines that emitted bytes.</summary>
    public bool IsData => !IsInstruction && Bytes.Length > 0;

    /// <summary>The emitted bytes as hex, e.g. "19 80 00 01".</summary>
    public string BytesText => string.Join(' ', Bytes.Select(b => b.ToString("X2")));
}

/// <summary>The result of assembling a program: the bytes to load, the symbol table and the listing.</summary>
public sealed class AssembledProgram
{
    public AssembledProgram(IReadOnlyList<ListingLine> listing, IReadOnlyDictionary<string, int> symbols, int entryPoint)
    {
        Listing = listing;
        Symbols = symbols;
        EntryPoint = entryPoint;
        Length = listing.Sum(l => l.Bytes.Length);
    }

    /// <summary>One entry per source line, in source order.</summary>
    public IReadOnlyList<ListingLine> Listing { get; }

    /// <summary>Labels and .equ constants with their values.</summary>
    public IReadOnlyDictionary<string, int> Symbols { get; }

    /// <summary>Address of the first instruction (where IP starts).</summary>
    public int EntryPoint { get; }

    /// <summary>Total number of bytes emitted.</summary>
    public int Length { get; }

    /// <summary>Renders the program as a full 64 KB memory image.</summary>
    public byte[] ToImage()
    {
        var image = new byte[Memory.Size];
        foreach (ListingLine line in Listing)
            line.Bytes.CopyTo(image, line.Address);
        return image;
    }

    /// <summary>Returns the listing line that produced the instruction at <paramref name="address"/>, if any.</summary>
    public ListingLine? FindByAddress(int address) =>
        Listing.FirstOrDefault(l => l.Bytes.Length > 0 && l.Address == address);

    /// <summary>Classic assembler listing: address, bytes and source side by side.</summary>
    public string FormatListing()
    {
        var text = new StringBuilder();
        foreach (ListingLine line in Listing)
        {
            string address = line.Bytes.Length > 0 ? $"{line.Address:X4}" : "    ";
            text.Append(address).Append("  ").Append(line.BytesText.PadRight(12)).Append("  ").AppendLine(line.Source);
        }
        return text.ToString();
    }
}
