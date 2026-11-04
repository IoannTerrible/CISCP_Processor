using CISCProcessorLibrary;

namespace CISCProcessorDemo;

/// <summary>
/// Console front end of the CISC-8 simulator.
/// Run without arguments for the interactive menu, or pass an assembly file / sample id to run it directly.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            ConsoleUi.ColorsEnabled = !options.NoColor;

            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            if (options.ListSamples)
            {
                ListSamples();
                return 0;
            }

            if (options.SampleId is not null)
            {
                SampleProgram? sample = SamplePrograms.Find(options.SampleId);
                if (sample is null)
                {
                    ConsoleUi.Error($"Unknown sample '{options.SampleId}'. Use --list to see the available ids.");
                    return 2;
                }
                return Runner.Run(sample.Name, sample.Source, options) ? 0 : 1;
            }

            if (options.FilePath is not null)
            {
                if (!File.Exists(options.FilePath))
                {
                    ConsoleUi.Error($"File not found: {options.FilePath}");
                    return 2;
                }
                string source = File.ReadAllText(options.FilePath);
                return Runner.Run(Path.GetFileName(options.FilePath), source, options) ? 0 : 1;
            }

            new InteractiveMenu(options).Run();
            return 0;
        }
        catch (ArgumentException ex)
        {
            ConsoleUi.Error(ex.Message);
            PrintUsage();
            return 2;
        }
    }

    private static void ListSamples()
    {
        ConsoleUi.Title("Sample programs");
        foreach (SampleProgram sample in SamplePrograms.All)
        {
            ConsoleUi.Write($"  {sample.Id,-10}", ConsoleColor.Cyan);
            Console.WriteLine($" {sample.Name} - {sample.Description}");
        }
    }

    private static void PrintUsage()
    {
        ConsoleUi.Title($"{SimulatorInfo.Name} processor simulator {SimulatorInfo.Version}");
        Console.WriteLine("Usage:");
        Console.WriteLine("  Demo                         interactive menu");
        Console.WriteLine("  Demo <program.asm> [options] assemble and run a file");
        Console.WriteLine("  Demo --sample <id> [options] run a built-in sample (see --list)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --trace        print every executed instruction with the registers after it");
        Console.WriteLine("  --micro        also print the micro-operations (bus cycles, ALU, register writes)");
        Console.WriteLine("  --delay <ms>   pause between instructions to watch the trace unfold");
        Console.WriteLine("  --max <n>      instruction budget before an endless loop is stopped (default 100000)");
        Console.WriteLine("  --no-color     plain output");
        Console.WriteLine("  --list         list the sample program ids");
        Console.WriteLine("  --help         this text");
    }
}
