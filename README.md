# CISC Processor Simulator 🚀

Welcome to the CISC Processor Simulator repository! This project provides a simulation of a Complex Instruction Set Computing (CISC) processor using C#. 

## Overview ℹ️

The CISC Processor Simulator includes: 

- Implementation of CISC instructions.
- A `CISCProcessor` class that emulates the functionality of a CISC processor, including stack management and instruction execution.
- A console application with a user interface to interact with the processor, allowing users to add custom instructions and view execution results.
- Color-coded console output for better readability and user experience.

## How to Use 🛠️

1. Clone the repository to your local machine.
2. Open the solution in Visual Studio or your preferred IDE.
3. Build and run the `CISCProcessorDemo` project.
4. Follow the on-screen instructions to add custom instructions, execute the program, and view the results.

## Example Output 🖥️

After running the program, you will see output similar to:

```
Program execution started
Executing instruction: LoadImmediateInstruction
Execution result: Accumulator = 10
Flags after instruction: Zero = False, Carry = False
Executing instruction: AddWithCarryInstruction
Execution result: Accumulator = 15
Flags after instruction: Zero = False, Carry = False
Execution results:
Accumulator: 42
Zero Flag: False
Carry Flag: True
```

The simulator provides a detailed log of instruction execution, accumulator values, and flag states, enhancing your understanding of how the CISC processor operates.