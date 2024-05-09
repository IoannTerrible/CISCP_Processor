using System;

namespace CISCProcessorLibrary
{
    public class StackProcessor : CISCProcessor
    {
        private int[] stack = new int[256]; 
        private int stackPointer = -1;

        public void Push(int value)
        {
            stackPointer++;
            if (stackPointer >= stack.Length)
            {
                throw new StackOverflowException("Stack overflow");
            }
            stack[stackPointer] = value;
        }

        public int Pop()
        {
            if (stackPointer < 0)
            {
                throw new InvalidOperationException("Stack underflow");
            }
            int value = stack[stackPointer];
            stackPointer--;
            return value;
        }
    }
}
