using System;
using System.Diagnostics;

namespace CISCProcessorLibrary
{
    public class CISCProcessor
    {
        public int Accumulator { get; set; }
        public int InstructionPointer { get; set; }
        public int AddressRegister { get; set; }
        public List<IInstruction> Instructions { get; set; } = new();
        public int[] Memory { get; } = new int[256];

        public int[] GeneralPurposeRegisters { get; } = new int[8]; // Example of 8 general-purpose registers
        public int FlagRegister { get; private set; } // Register for flags

        private bool[] flags = new bool[Enum.GetNames(typeof(Flag)).Length];

        public void SetFlag(Flag flag, bool value)
        {
            flags[(int)flag] = value;
            UpdateFlagRegister();
        }

        public bool IsFlagSet(Flag flag)
        {
            return flags[(int)flag];
        }

        public void UpdateFlags(int result)
        {
            SetFlag(Flag.Zero, result == 0);
            SetFlag(Flag.Carry, result < 0 || result > 255);
        }

        public void ExecuteInstructions(List<IInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                Debug.WriteLine("Executing instruction: " + instruction.GetType().Name);
                instruction.Execute();
                Debug.WriteLine("Execution result: Accumulator = " + this.Accumulator);
                Debug.WriteLine("Flags after instruction: Zero = " + this.IsFlagSet(Flag.Zero) + ", Carry = " + this.IsFlagSet(Flag.Carry));
            }
        }
        public void AddInstruction(IInstruction instruction)
        {
            Instructions.Add(instruction);
        }
        private void UpdateFlagRegister()
        {
            FlagRegister = 0;
            if (IsFlagSet(Flag.Zero)) FlagRegister |= 1 << 0;
            if (IsFlagSet(Flag.Carry)) FlagRegister |= 1 << 1;
        }
        
    }
}
