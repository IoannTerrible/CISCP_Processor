using CISCProcessorLibrary;

namespace CISCProcessorLibrary.Tests;

internal static class TestExtensions
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CISCProcessor, AssembledProgram> Programs = new();

    /// <summary>Loads source and remembers the symbol table so tests can look up labels.</summary>
    public static AssembledProgram LoadAndRemember(this CISCProcessor cpu, string source)
    {
        AssembledProgram program = cpu.LoadProgram(source);
        Programs.AddOrUpdate(cpu, program);
        return program;
    }

    public static int LoadedSymbol(this CISCProcessor cpu, string name)
    {
        if (!Programs.TryGetValue(cpu, out AssembledProgram? program))
            throw new InvalidOperationException("No program was loaded through LoadAndRemember.");
        return program.Symbols[name];
    }
}
