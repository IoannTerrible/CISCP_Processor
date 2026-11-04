using System.Text;
using CISCProcessorLibrary;

namespace CISCProcessorDemo;

/// <summary>Colored console helpers shared by the runner and the menu.</summary>
internal static class ConsoleUi
{
    public static bool ColorsEnabled { get; set; } = true;

    public static void Write(string text, ConsoleColor color)
    {
        if (ColorsEnabled) Console.ForegroundColor = color;
        Console.Write(text);
        if (ColorsEnabled) Console.ResetColor();
    }

    public static void WriteLine(string text, ConsoleColor color)
    {
        Write(text, color);
        Console.WriteLine();
    }

    public static void Title(string text)
    {
        Console.WriteLine();
        WriteLine($"== {text} ==", ConsoleColor.Blue);
    }

    public static void Error(string text) => WriteLine($"error: {text}", ConsoleColor.Red);

    public static void Success(string text) => WriteLine(text, ConsoleColor.Green);

    /// <summary>Prints the assembler listing (address, bytes, source).</summary>
    public static void PrintListing(AssembledProgram program)
    {
        Title("Listing");
        foreach (ListingLine line in program.Listing)
        {
            Write(line.Bytes.Length > 0 ? $"{line.Address:X2}  " : "    ", ConsoleColor.DarkCyan);
            Write(line.BytesText.PadRight(10), ConsoleColor.DarkYellow);
            Console.WriteLine(line.Source);
        }
        Console.WriteLine();
        Console.WriteLine($"{program.Length} bytes, entry point 0x{program.EntryPoint:X2}, {program.Symbols.Count} symbol(s)");
    }

    /// <summary>Prints one trace line; registers that changed since <paramref name="previous"/> are highlighted.</summary>
    public static void PrintStep(StepTrace step, ProcessorState? previous)
    {
        ProcessorState s = step.StateAfter;
        Write($"{step.Address:X2}  ", ConsoleColor.DarkCyan);
        Write(step.BytesText.PadRight(9), ConsoleColor.DarkYellow);
        Write(step.Disassembly.PadRight(20), ConsoleColor.White);
        Write("| ", ConsoleColor.DarkGray);

        Register("A", s.Accumulator, previous?.Accumulator);
        for (int i = 0; i < s.Registers.Count; i++)
            Register($"R{i}", s.Registers[i], previous?.Registers[i]);
        Register("IP", s.InstructionPointer, previous?.InstructionPointer);
        Register("SP", s.StackPointer, previous?.StackPointer);

        bool flagsChanged = previous is not null && previous.FlagRegister != s.FlagRegister;
        Write(s.FlagsText + " ", flagsChanged ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
        Write($"{step.Cycles,2}c", ConsoleColor.DarkGray);
        Console.WriteLine();

        static void Register(string name, byte value, byte? before)
        {
            bool changed = before.HasValue && before.Value != value;
            Write($"{name}={value:X2} ", changed ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
        }
    }

    /// <summary>Prints the micro-operations of one step, indented under the trace line.</summary>
    public static void PrintMicroOps(StepTrace step)
    {
        foreach (MicroOp op in step.MicroOps)
        {
            // MAR / MDR change on every bus cycle; the bus line itself already shows the transfer.
            if (op.Kind == MicroOpKind.Register && op.Name is "MAR" or "MDR") continue;

            ConsoleColor color = op.Kind switch
            {
                MicroOpKind.Fetch or MicroOpKind.Read => ConsoleColor.DarkCyan,
                MicroOpKind.Write => ConsoleColor.DarkYellow,
                MicroOpKind.Alu => ConsoleColor.Magenta,
                MicroOpKind.Register or MicroOpKind.Flags => ConsoleColor.DarkGray,
                MicroOpKind.Jump => ConsoleColor.Cyan,
                MicroOpKind.Output => ConsoleColor.Green,
                MicroOpKind.Fault => ConsoleColor.Red,
                _ => ConsoleColor.Gray,
            };
            WriteLine($"        {op}", color);
        }
    }

    /// <summary>Prints every register of the processor.</summary>
    public static void PrintRegisters(ProcessorState state)
    {
        Title("Registers");
        Console.WriteLine($"A  = 0x{state.Accumulator:X2} ({state.Accumulator})");
        var registers = new StringBuilder();
        for (int i = 0; i < state.Registers.Count; i++)
            registers.Append($"R{i}=0x{state.Registers[i]:X2} ");
        Console.WriteLine(registers.ToString().TrimEnd());
        Console.WriteLine($"IP = 0x{state.InstructionPointer:X2}  SP = 0x{state.StackPointer:X2}  IR = 0x{state.InstructionRegister:X2}  MAR = 0x{state.MemoryAddressRegister:X2}  MDR = 0x{state.MemoryDataRegister:X2}");
        Console.WriteLine($"FLAGS = {state.FlagsText}  (Z={Bit(state, Flag.Zero)} C={Bit(state, Flag.Carry)} N={Bit(state, Flag.Negative)} V={Bit(state, Flag.Overflow)})");
        Console.WriteLine($"{state.InstructionsExecuted} instructions, {state.Cycles} clock cycles");

        static int Bit(ProcessorState s, Flag f) => s.IsFlagSet(f) ? 1 : 0;
    }

    /// <summary>Prints memory as a 16 x 16 hex dump. Non-zero bytes and the stack area are highlighted.</summary>
    public static void PrintMemory(CISCProcessor cpu)
    {
        Title("Memory");
        Write("     ", ConsoleColor.DarkGray);
        for (int col = 0; col < 16; col++)
            Write($"{col:X2} ", ConsoleColor.DarkGray);
        Console.WriteLine();

        for (int row = 0; row < Memory.Size / 16; row++)
        {
            Write($"{row * 16:X2}:  ", ConsoleColor.DarkGray);
            for (int col = 0; col < 16; col++)
            {
                int address = row * 16 + col;
                byte value = cpu.Memory[address];
                ConsoleColor color = address > cpu.StackPointer ? ConsoleColor.Magenta
                    : value != 0 ? ConsoleColor.White
                    : ConsoleColor.DarkGray;
                Write($"{value:X2} ", color);
            }
            Console.WriteLine();
        }
        Console.WriteLine("(magenta = bytes on the stack)");
    }

    /// <summary>Prints the instruction set as a reference table.</summary>
    public static void PrintInstructionSet(InstructionSet set)
    {
        Title("Instruction set reference");
        Write("Op  Syntax              Len Cyc  Description", ConsoleColor.DarkGray);
        Console.WriteLine();
        foreach (IInstruction instruction in set.All)
        {
            Write($"{instruction.Opcode:X2}  ", ConsoleColor.DarkCyan);
            Write(instruction.Syntax.Template.PadRight(20), ConsoleColor.White);
            Console.WriteLine($"{instruction.Length,2}  {instruction.Cycles,2}   {instruction.Description}");
        }
        Console.WriteLine();
        Console.WriteLine("Operands: A, R0..R7, SP, FLAGS, imm8 (number, 'c', label), [addr], [Rn], [addr+Rn]");
        Console.WriteLine("Directives: .org addr | .db v, \"text\" | .ds count | .equ NAME, value");
    }
}
