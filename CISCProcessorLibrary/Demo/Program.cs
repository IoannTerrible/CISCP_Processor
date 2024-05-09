using System;
using CISCProcessorLibrary;

namespace CISCProcessorDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            StackProcessor processor = new StackProcessor(); // Use StackProcessor instead of CISCProcessor
            InitializeRegisters(processor); // Initialize registers

            // Push values onto the stack
            processor.Push(100);
            processor.Push(200);
            processor.Push(300);

            // Pop values from the stack
            int value1 = processor.Pop();
            int value2 = processor.Pop();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n-- Popped values from the stack --");
            Console.WriteLine($"Value 1: {value1}");
            Console.WriteLine($"Value 2: {value2}");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\n-- Program execution started --");
            Console.ResetColor();

            bool addMoreInstructions = true;
            List<string> userChoices = new List<string>();

            while (addMoreInstructions)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nChoose an instruction to add:");
                Console.WriteLine("1. Load Immediate");
                Console.WriteLine("2. Add with Carry");
                Console.WriteLine("3. Subtract with Borrow");
                Console.WriteLine("4. Jump If Zero");
                Console.WriteLine("5. Store To Memory");
                Console.WriteLine("6. Exit");
                Console.ResetColor();

                Console.Write("Enter your choice: ");
                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        Console.Write("Enter the immediate value: ");
                        int immediateValue = Convert.ToInt32(Console.ReadLine());
                        processor.AddInstruction(new LoadImmediateInstruction(processor, immediateValue));
                        userChoices.Add($"Load Immediate with value {immediateValue}");
                        break;
                    case "2":
                        Console.Write("Enter the value to add with carry: ");
                        int addValue = Convert.ToInt32(Console.ReadLine());
                        processor.AddInstruction(new AddWithCarryInstruction(processor, addValue));
                        userChoices.Add($"Add with Carry by {addValue}");
                        break;
                    case "3":
                        Console.Write("Enter the value to subtract with borrow: ");
                        int subtractValue = Convert.ToInt32(Console.ReadLine());
                        processor.AddInstruction(new SubtractWithBorrowInstruction(processor, subtractValue));
                        userChoices.Add($"Subtract with Borrow by {subtractValue}");
                        break;
                    case "4":
                        Console.Write("Enter the jump offset: ");
                        int jumpOffset = Convert.ToInt32(Console.ReadLine());
                        processor.AddInstruction(new JumpIfZeroInstruction(processor, jumpOffset));
                        userChoices.Add($"Jump If Zero with offset {jumpOffset}");
                        break;
                    case "5":
                        processor.AddInstruction(new StoreToMemoryInstruction(processor));
                        userChoices.Add("Store To Memory");
                        break;
                    case "6":
                        addMoreInstructions = false;
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid choice. Please try again.");
                        Console.ResetColor();
                        break;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nInstructions added by the user:");
            foreach (var choice in userChoices)
            {
                Console.WriteLine(choice);
            }
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\n-- Executing added instructions --");
            Console.ResetColor();

            processor.ExecuteInstructions(processor.Instructions);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n-- Execution results --");
            Console.WriteLine($"Accumulator: {processor.Accumulator}");
            Console.WriteLine($"Zero Flag: {processor.IsFlagSet(Flag.Zero)}");
            Console.WriteLine($"Carry Flag: {processor.IsFlagSet(Flag.Carry)}");
            Console.ResetColor();
        }

        static void InitializeRegisters(CISCProcessor processor)
        {
            for (int i = 0; i < processor.GeneralPurposeRegisters.Length; i++)
            {
                processor.GeneralPurposeRegisters[i] = i * 10;
            }
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\n-- Initialized General Purpose Registers --");
            Console.ResetColor();
            for (int i = 0; i < processor.GeneralPurposeRegisters.Length; i++)
            {
                Console.WriteLine($"Register {i}: {processor.GeneralPurposeRegisters[i]}");
            }
        }
    }
}
