using System.Text;
using CISCProcessorLibrary;

namespace CISCProcessorDemo;

/// <summary>The interactive console menu shown when the demo starts without arguments.</summary>
internal sealed class InteractiveMenu
{
    private readonly Options options;

    public InteractiveMenu(Options options)
    {
        this.options = options;
    }

    public void Run()
    {
        ConsoleUi.Title($"{SimulatorInfo.Name} processor simulator {SimulatorInfo.Version}");
        Console.WriteLine("An 8 bit CISC machine: accumulator, R0..R7, 256 bytes of memory, hardware stack, Z C N V flags.");

        while (true)
        {
            Console.WriteLine();
            ConsoleUi.WriteLine("1. Run a sample program", ConsoleColor.Yellow);
            ConsoleUi.WriteLine("2. Write and run your own program", ConsoleColor.Yellow);
            ConsoleUi.WriteLine("3. Instruction set reference", ConsoleColor.Yellow);
            ConsoleUi.WriteLine("4. Exit", ConsoleColor.Yellow);
            string? choice = Prompt("Choose: ");
            if (choice is null) return;

            switch (choice.Trim())
            {
                case "1":
                    RunSample();
                    break;
                case "2":
                    RunOwnProgram();
                    break;
                case "3":
                    ConsoleUi.PrintInstructionSet(InstructionSet.Default);
                    break;
                case "4" or "q" or "exit":
                    return;
                default:
                    ConsoleUi.Error("Invalid choice. Please try again.");
                    break;
            }
        }
    }

    private void RunSample()
    {
        ConsoleUi.Title("Sample programs");
        for (int i = 0; i < SamplePrograms.All.Count; i++)
        {
            SampleProgram sample = SamplePrograms.All[i];
            ConsoleUi.Write($"{i + 1}. {sample.Name}", ConsoleColor.Cyan);
            Console.WriteLine($" - {sample.Description}");
        }

        string? choice = Prompt("Sample number: ");
        if (choice is null) return;
        if (!int.TryParse(choice, out int index) || index < 1 || index > SamplePrograms.All.Count)
        {
            ConsoleUi.Error("Invalid sample number.");
            return;
        }

        SampleProgram selected = SamplePrograms.All[index - 1];
        if (!AskRunSettings()) return;
        Runner.Run(selected.Name, selected.Source, options);
    }

    private void RunOwnProgram()
    {
        ConsoleUi.Title("Your program");
        Console.WriteLine("Type assembly lines. Finish with an empty line. Example:");
        ConsoleUi.WriteLine("        MOV A, 40", ConsoleColor.DarkGray);
        ConsoleUi.WriteLine("        ADD A, 2", ConsoleColor.DarkGray);
        ConsoleUi.WriteLine("        OUT 0, A", ConsoleColor.DarkGray);
        ConsoleUi.WriteLine("        HLT", ConsoleColor.DarkGray);
        Console.WriteLine();

        var source = new StringBuilder();
        int lineNumber = 1;
        while (true)
        {
            string? line = Prompt($"{lineNumber,3}: ");
            if (string.IsNullOrWhiteSpace(line)) break;
            source.AppendLine(line);
            lineNumber++;
        }

        if (source.Length == 0)
        {
            ConsoleUi.Error("No program entered.");
            return;
        }

        if (!AskRunSettings()) return;
        Runner.Run("your program", source.ToString(), options);
    }

    private bool AskRunSettings()
    {
        string? trace = Prompt("Trace every instruction? [Y/n]: ");
        if (trace is null) return false;
        options.Trace = !trace.Trim().StartsWith("n", StringComparison.OrdinalIgnoreCase);

        if (options.Trace)
        {
            string? micro = Prompt("Show micro-operations (bus cycles, ALU, registers)? [y/N]: ");
            if (micro is null) return false;
            options.MicroOps = micro.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);

            string? delay = Prompt("Delay between instructions in ms [0]: ");
            if (delay is null) return false;
            options.DelayMilliseconds = int.TryParse(delay, out int ms) && ms >= 0 ? ms : 0;
        }
        return true;
    }

    private static string? Prompt(string text)
    {
        ConsoleUi.Write(text, ConsoleColor.Cyan);
        return Console.ReadLine();
    }
}
