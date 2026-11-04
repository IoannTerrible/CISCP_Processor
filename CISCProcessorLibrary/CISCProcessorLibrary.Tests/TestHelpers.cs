using CISCProcessorLibrary;

namespace CISCProcessorLibrary.Tests;

internal static class TestHelpers
{
    /// <summary>Assembles, loads and runs <paramref name="source"/> on a fresh processor.</summary>
    public static CISCProcessor Run(string source, int maxInstructions = 10_000)
    {
        var cpu = new CISCProcessor();
        cpu.LoadAndRemember(source);
        cpu.Run(maxInstructions);
        return cpu;
    }

    /// <summary>Runs <paramref name="source"/> and returns everything written to the output ports.</summary>
    public static string Output(string source)
    {
        var cpu = new CISCProcessor();
        var text = new System.Text.StringBuilder();
        cpu.OutputWritten += (_, e) =>
        {
            text.Append(e.Text);
            if (e.Port == Ports.Numeric) text.Append(' ');
        };
        cpu.LoadProgram(source);
        cpu.Run();
        return text.ToString().TrimEnd();
    }

    public static byte[] Bytes(string source) =>
        Assembler.Assemble(source).Listing.SelectMany(l => l.Bytes).ToArray();
}
