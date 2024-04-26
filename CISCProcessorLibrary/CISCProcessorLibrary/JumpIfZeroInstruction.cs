using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CISCProcessorLibrary
{
    public class JumpIfZeroInstruction : IInstruction
    {
        private readonly CISCProcessor processor;
        private readonly int address;

        public JumpIfZeroInstruction(CISCProcessor processor, int address)
        {
            this.processor = processor;
            this.address = address;
        }

        public void Execute()
        {
            if (processor.IsFlagSet(Flag.Zero))
            {
                processor.InstructionPointer = address;
            }
        }
    }
}
