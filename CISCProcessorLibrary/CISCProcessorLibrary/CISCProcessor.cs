using System;

namespace CISCProcessorLibrary
{

    public class CISCProcessor
    {
        public int Accumulator { get; set; }
        public int InstructionPointer { get; set; }
        public int AddressRegister { get; set; }
        public int[] Memory { get; } = new int[256]; // Example memory size

        private bool[] flags = new bool[Enum.GetNames(typeof(Flag)).Length];

        public void SetFlag(Flag flag, bool value)
        {
            flags[(int)flag] = value;
        }

        public bool IsFlagSet(Flag flag)
        {
            return flags[(int)flag];
        }

        public void UpdateFlags(int result)
        {
            SetFlag(Flag.Zero, result == 0);
            SetFlag(Flag.Carry, result < 0 || result > 255); // Assuming 8-bit arithmetic
        }
    }
}
