using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CISCProcessorLibrary
{
    public class LoadImmediateInstruction : IInstruction
    {
        private readonly CISCProcessor processor;
        private readonly int value;

        public LoadImmediateInstruction(CISCProcessor processor, int value)
        {
            this.processor = processor;
            this.value = value;
        }

        public void Execute()
        {
            processor.Accumulator = value;
        }
    }
}
