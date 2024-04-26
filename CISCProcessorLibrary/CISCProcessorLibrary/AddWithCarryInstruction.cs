using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CISCProcessorLibrary
{
    public class AddWithCarryInstruction : IInstruction
    {
        private readonly CISCProcessor processor;
        private readonly int value;

        public AddWithCarryInstruction(CISCProcessor processor, int value)
        {
            this.processor = processor;
            this.value = value;
        }

        public void Execute()
        {
            int result = processor.Accumulator + value;
            if (processor.IsFlagSet(Flag.Carry)) result++;
            processor.Accumulator = result;
            processor.UpdateFlags(result);
        }
    }
}
