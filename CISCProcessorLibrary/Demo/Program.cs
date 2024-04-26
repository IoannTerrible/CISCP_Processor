using System;
using System.Diagnostics;
using CISCProcessorLibrary;

namespace CISCProcessorDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            CISCProcessor processor = new CISCProcessor();

            IInstruction[] instructions = new IInstruction[]
            {
                new LoadImmediateInstruction(processor, 10),
                new AddWithCarryInstruction(processor, 5),
                new SubtractWithBorrowInstruction(processor, 3),
                new JumpIfZeroInstruction(processor, 5), // Assuming address 5
                new StoreToMemoryInstruction(processor)
            };

            Debug.WriteLine("Program execution started");

            foreach (var instruction in instructions)
            {
                Debug.WriteLine("Executing instruction: " + instruction.GetType().Name);
                instruction.Execute();
                Debug.WriteLine("Execution result: Accumulator = " + processor.Accumulator);
                Debug.WriteLine("Flags after instruction: Zero = " + processor.IsFlagSet(Flag.Zero) + ", Carry = " + processor.IsFlagSet(Flag.Carry));
            }

            Debug.WriteLine("Execution results:");
            Debug.WriteLine("Accumulator: " + processor.Accumulator);
            Debug.WriteLine("Zero Flag: " + processor.IsFlagSet(Flag.Zero));
            Debug.WriteLine("Carry Flag: " + processor.IsFlagSet(Flag.Carry));

        }
    }
}
