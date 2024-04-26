using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CISCProcessorLibrary
{
    public class StoreToMemoryInstruction : IInstruction
    {
        private readonly CISCProcessor processor;

        public StoreToMemoryInstruction(CISCProcessor processor)
        {
            this.processor = processor;
        }

        public void Execute()
        {
            processor.Memory[processor.AddressRegister] = processor.Accumulator;
        }
    }
}
