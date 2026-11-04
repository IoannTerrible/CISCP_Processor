using System.Text;
using CISCProcessorLibrary;
using CISCProcessorLibrary.Timing;

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
            Write(line.Bytes.Length > 0 ? $"{line.Address:X4}  " : "      ", ConsoleColor.DarkCyan);
            Write(line.BytesText.PadRight(13), ConsoleColor.DarkYellow);
            Console.WriteLine(line.Source);
        }
        Console.WriteLine();
        Console.WriteLine($"{program.Length} bytes, entry point 0x{program.EntryPoint:X4}, {program.Symbols.Count} symbol(s)");
    }

    /// <summary>Prints one trace line; registers that changed since <paramref name="previous"/> are highlighted.</summary>
    public static void PrintStep(StepTrace step, ProcessorState? previous)
    {
        ProcessorState s = step.StateAfter;
        Write($"{step.Address:X4}  ", ConsoleColor.DarkCyan);
        Write(step.BytesText.PadRight(12), ConsoleColor.DarkYellow);
        Write(step.Disassembly.PadRight(22), step.Kind == StepKind.Instruction ? ConsoleColor.White : ConsoleColor.Magenta);
        Write("| ", ConsoleColor.DarkGray);

        Register("A", s.Accumulator, previous?.Accumulator);
        for (int i = 0; i < s.Registers.Count; i++)
            Register($"R{i}", s.Registers[i], previous?.Registers[i]);
        Register16("SI", s.SourceIndex, previous?.SourceIndex);
        Register16("DI", s.DestinationIndex, previous?.DestinationIndex);
        Register16("BP", s.BasePointer, previous?.BasePointer);
        Register16("SP", s.StackPointer, previous?.StackPointer);
        Register16("IP", s.InstructionPointer, previous?.InstructionPointer);

        bool flagsChanged = previous is not null && previous.FlagRegister != s.FlagRegister;
        Write(s.FlagsText + " ", flagsChanged ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
        Write($"{step.Cycles,3}c", ConsoleColor.DarkGray);
        Console.WriteLine();

        static void Register(string name, byte value, byte? before)
        {
            bool changed = before.HasValue && before.Value != value;
            Write($"{name}={value:X2} ", changed ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
        }

        static void Register16(string name, ushort value, ushort? before)
        {
            bool changed = before.HasValue && before.Value != value;
            Write($"{name}={value:X4} ", changed ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
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
                MicroOpKind.Jump or MicroOpKind.Interrupt => ConsoleColor.Cyan,
                MicroOpKind.Output or MicroOpKind.Input => ConsoleColor.Green,
                MicroOpKind.Timing => ConsoleColor.DarkMagenta,
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
        Console.WriteLine($"SI = 0x{state.SourceIndex:X4}  DI = 0x{state.DestinationIndex:X4}  BP = 0x{state.BasePointer:X4}  SP = 0x{state.StackPointer:X4}  IP = 0x{state.InstructionPointer:X4}");
        Console.WriteLine($"IR = 0x{state.InstructionRegister:X2}  MAR = 0x{state.MemoryAddressRegister:X4}  MDR = 0x{state.MemoryDataRegister:X2}  pending IRQ mask = 0x{state.PendingInterrupts:X2}");
        Console.WriteLine($"FLAGS = {state.FlagsText}  (Z={Bit(state, Flag.Zero)} C={Bit(state, Flag.Carry)} N={Bit(state, Flag.Negative)} V={Bit(state, Flag.Overflow)} I={Bit(state, Flag.Interrupt)})");
        Console.WriteLine($"{state.InstructionsExecuted} instructions, {state.Cycles} clock cycles");

        static int Bit(ProcessorState s, Flag f) => s.IsFlagSet(f) ? 1 : 0;
    }

    /// <summary>Prints the timing model and its counters.</summary>
    public static void PrintTiming(CISCProcessor cpu)
    {
        Title("Micro-architecture");
        TimingModel timing = cpu.Timing;
        WriteLine(timing.Name, ConsoleColor.White);
        Console.WriteLine(timing.Description);
        foreach ((string key, long value) in timing.Statistics.OrderBy(p => p.Key))
            Console.WriteLine($"  {key,-24} {value}");
    }

    /// <summary>Prints every used 256 byte page of memory as a hex dump. Stack bytes are highlighted.</summary>
    public static void PrintMemory(CISCProcessor cpu)
    {
        Title("Memory (used pages)");
        bool any = false;
        for (int page = 0; page < Memory.Size / Memory.PageSize; page++)
        {
            int pageStart = page * Memory.PageSize;
            bool holdsStack = cpu.StackPointer < pageStart + Memory.PageSize && CISCProcessor.InitialStackPointer > pageStart;
            if (!cpu.Memory.IsPageUsed(page) && !holdsStack) continue;
            any = true;

            Write($"page 0x{page:X2}  ", ConsoleColor.DarkGray);
            for (int col = 0; col < 16; col++)
                Write($"{col:X2} ", ConsoleColor.DarkGray);
            Console.WriteLine();

            for (int row = 0; row < 16; row++)
            {
                int rowStart = pageStart + row * 16;
                if (!RowUsed(cpu, rowStart)) continue;
                Write($"{rowStart:X4}:  ", ConsoleColor.DarkGray);
                for (int col = 0; col < 16; col++)
                {
                    int address = rowStart + col;
                    byte value = cpu.Memory[address];
                    ConsoleColor color = address >= cpu.StackPointer && address < CISCProcessor.InitialStackPointer ? ConsoleColor.Magenta
                        : address >= CISCProcessor.InterruptVectorTable ? ConsoleColor.Cyan
                        : value != 0 ? ConsoleColor.White
                        : ConsoleColor.DarkGray;
                    Write($"{value:X2} ", color);
                }
                Console.WriteLine();
            }
        }
        if (!any) Console.WriteLine("(memory is empty)");
        Console.WriteLine("(magenta = bytes on the stack, cyan = interrupt vector table, rows that are all zero are skipped)");

        static bool RowUsed(CISCProcessor cpu, int rowStart)
        {
            for (int i = 0; i < 16; i++)
                if (cpu.Memory[rowStart + i] != 0) return true;
            return cpu.StackPointer < rowStart + 16 && CISCProcessor.InitialStackPointer > rowStart;
        }
    }

    /// <summary>Prints the instruction set as a reference table.</summary>
    public static void PrintInstructionSet(InstructionSet set)
    {
        Title("Instruction set reference");
        Write("Op  Syntax                Len  Description", ConsoleColor.DarkGray);
        Console.WriteLine();
        foreach (IInstruction instruction in set.All)
        {
            Write($"{instruction.Opcode:X2}  ", ConsoleColor.DarkCyan);
            Write(instruction.Syntax.Template.PadRight(22), ConsoleColor.White);
            Console.WriteLine($"{instruction.Length,2}   {instruction.Description}");
        }
        Console.WriteLine();
        Console.WriteLine("Operands: A, R0..R7, SI, DI, BP, SP, FLAGS, imm8 / imm16 (number, 'c', label), [addr], [Rn] (zero page),");
        Console.WriteLine("          [addr+Rn], [SI] / [DI] / [BP] / [SP], [BP+disp] / [BP-disp]");
        Console.WriteLine("Directives: .org addr | .db v, \"text\" | .dw word, label | .ds count | .equ NAME, value");
        Console.WriteLine("Ports: OUT 0 = number, OUT 1 = character, 2/3 = timer period, 4 = timer control, IN 1 = input, IN 6 = input status");
    }
}
