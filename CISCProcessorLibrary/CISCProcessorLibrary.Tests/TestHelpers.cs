using CISCProcessorLibrary;
using CISCProcessorLibrary.Timing;

namespace CISCProcessorLibrary.Tests;

internal static class TestHelpers
{
    /// <summary>Assembles, loads and runs <paramref name="source"/> on a fresh processor.</summary>
    public static CISCProcessor Run(string source, Microarchitecture microarchitecture = Microarchitecture.Classic1975, int maxSteps = 500_000)
    {
        var cpu = new CISCProcessor(microarchitecture);
        cpu.LoadAndRemember(source);
        cpu.Run(maxSteps);
        return cpu;
    }

    /// <summary>Runs <paramref name="source"/> and returns everything written to the output ports.</summary>
    public static string Output(string source, string? input = null, Microarchitecture microarchitecture = Microarchitecture.Classic1975)
    {
        var cpu = new CISCProcessor(microarchitecture);
        var text = new System.Text.StringBuilder();
        cpu.OutputWritten += (_, e) =>
        {
            text.Append(e.Text);
            if (e.Port == Ports.Numeric) text.Append(' ');
        };
        cpu.LoadProgram(source);
        if (input is not null) cpu.InputDevice.Enqueue(input);
        cpu.Run(500_000);
        return text.ToString().TrimEnd();
    }

    public static byte[] Bytes(string source) =>
        Assembler.Assemble(source).Listing.SelectMany(l => l.Bytes).ToArray();
}
