using System.Text;
using CISCProcessorLibrary;

namespace CISCProcessorDemo;

/// <summary>Assembles a program, runs it and prints what happened.</summary>
internal static class Runner
{
    /// <summary>Returns true when the program halted normally.</summary>
    public static bool Run(string name, string source, Options options)
    {
        ConsoleUi.Title($"Assembling {name}");
        AssembledProgram program;
        try
        {
            program = Assembler.Assemble(source);
        }
        catch (AssemblyException ex)
        {
            ConsoleUi.Error(ex.Message);
            return false;
        }

        ConsoleUi.PrintListing(program);

        var cpu = new CISCProcessor();
        cpu.LoadProgram(program);

        var output = new StringBuilder();
        var pendingOutput = new List<OutputEventArgs>();
        cpu.OutputWritten += (_, e) =>
        {
            output.Append(e.Text);
            if (e.Port == Ports.Numeric) output.Append(' ');
            pendingOutput.Add(e);
        };

        if (options.Trace)
        {
            ConsoleUi.Title("Execution trace");
            ConsoleUi.WriteLine("At  Bytes    Instruction         | registers after the instruction (changed = yellow)", ConsoleColor.DarkGray);
            ProcessorState? previous = cpu.GetState();
            cpu.InstructionExecuted += (_, step) =>
            {
                ConsoleUi.PrintStep(step, previous);
                if (options.MicroOps) ConsoleUi.PrintMicroOps(step);
                foreach (OutputEventArgs e in pendingOutput)
                    ConsoleUi.WriteLine($"        OUT port {e.Port}: {e.Text}", ConsoleColor.Green);
                pendingOutput.Clear();
                previous = step.StateAfter;
                if (options.DelayMilliseconds > 0) Thread.Sleep(options.DelayMilliseconds);
            };
        }
        else
        {
            ConsoleUi.Title("Running");
        }

        RunResult result = cpu.Run(options.MaxInstructions);

        Console.WriteLine();
        ConsoleUi.Write("Output: ", ConsoleColor.DarkGray);
        ConsoleUi.WriteLine(output.Length > 0 ? output.ToString().TrimEnd() : "(nothing was written to the output ports)", ConsoleColor.Green);

        switch (result.Reason)
        {
            case StopReason.Halted:
                ConsoleUi.Success($"Halted normally after {result.InstructionsExecuted} instructions and {result.Cycles} clock cycles.");
                break;
            case StopReason.Fault:
                ConsoleUi.Error($"{result.Fault}: {result.FaultMessage} (after {result.InstructionsExecuted} instructions)");
                break;
            case StopReason.InstructionLimit:
                ConsoleUi.WriteLine($"Stopped after {result.InstructionsExecuted} instructions without reaching HLT (endless loop?).", ConsoleColor.Yellow);
                break;
        }

        ConsoleUi.PrintRegisters(result.FinalState);
        ConsoleUi.PrintMemory(cpu);
        return result.Reason == StopReason.Halted;
    }
}
