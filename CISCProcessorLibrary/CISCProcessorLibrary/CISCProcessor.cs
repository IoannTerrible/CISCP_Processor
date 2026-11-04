namespace CISCProcessorLibrary;

/// <summary>
/// An 8 bit CISC processor ("CISC-8"): accumulator A, eight general purpose registers R0..R7,
/// an 8 bit instruction pointer, a hardware stack that lives in main memory, a four flag status
/// register and a variable length instruction set with several addressing modes.
/// The control unit runs a classic fetch - decode - execute cycle and reports every micro-operation
/// it performs, which is what the console demo and the web visualiser render.
/// </summary>
public class CISCProcessor
{
    /// <summary>Number of general purpose registers.</summary>
    public const int RegisterCount = 8;

    /// <summary>Value of SP after reset. The stack grows downwards from here.</summary>
    public const byte InitialStackPointer = 0xFF;

    private readonly byte[] registers = new byte[RegisterCount];
    private byte accumulator;
    private byte instructionPointer;
    private byte stackPointer = InitialStackPointer;
    private byte flagRegister;
    private byte instructionRegister;
    private byte memoryAddressRegister;
    private byte memoryDataRegister;
    private int extraCycles;
    private List<MicroOp>? currentMicroOps;

    public CISCProcessor()
        : this(InstructionSet.Default)
    {
    }

    public CISCProcessor(InstructionSet instructionSet)
    {
        InstructionSet = instructionSet ?? throw new ArgumentNullException(nameof(instructionSet));
        Memory = new Memory();
        Alu = new ArithmeticLogicUnit(this);
    }

    /// <summary>The instruction set the decoder uses.</summary>
    public InstructionSet InstructionSet { get; }

    /// <summary>Main memory (256 bytes shared by code, data and stack).</summary>
    public Memory Memory { get; }

    /// <summary>The arithmetic logic unit.</summary>
    public ArithmeticLogicUnit Alu { get; }

    /// <summary>Accumulator A - the implicit operand of every ALU instruction.</summary>
    public byte Accumulator
    {
        get => accumulator;
        set => ChangeRegister("A", ref accumulator, value);
    }

    /// <summary>Address of the next instruction to fetch.</summary>
    public byte InstructionPointer
    {
        get => instructionPointer;
        set => ChangeRegister("IP", ref instructionPointer, value);
    }

    /// <summary>Address of the next free stack slot. Push stores at SP and decrements it.</summary>
    public byte StackPointer
    {
        get => stackPointer;
        set => ChangeRegister("SP", ref stackPointer, value);
    }

    /// <summary>Status register. Bit positions are defined by <see cref="Flag"/>.</summary>
    public byte FlagRegister
    {
        get => flagRegister;
        set
        {
            byte masked = (byte)(value & 0x0F);
            if (masked == flagRegister) return;
            byte before = flagRegister;
            flagRegister = masked;
            Record(MicroOp.Flags(before, masked));
        }
    }

    /// <summary>Opcode of the instruction currently being executed.</summary>
    public byte InstructionRegister
    {
        get => instructionRegister;
        private set => ChangeRegister("IR", ref instructionRegister, value);
    }

    /// <summary>Last address placed on the address bus.</summary>
    public byte MemoryAddressRegister
    {
        get => memoryAddressRegister;
        private set => ChangeRegister("MAR", ref memoryAddressRegister, value);
    }

    /// <summary>Last byte transferred over the data bus.</summary>
    public byte MemoryDataRegister
    {
        get => memoryDataRegister;
        private set => ChangeRegister("MDR", ref memoryDataRegister, value);
    }

    /// <summary>Read-only view of R0..R7. Use <see cref="ReadRegister"/> / <see cref="WriteRegister"/> to change them.</summary>
    public IReadOnlyList<byte> Registers => registers;

    /// <summary>Phase of the instruction cycle the control unit is in.</summary>
    public CyclePhase Phase { get; private set; } = CyclePhase.Idle;

    /// <summary>Clock cycles consumed since reset.</summary>
    public long Cycles { get; private set; }

    /// <summary>Instructions completed since reset.</summary>
    public long InstructionsExecuted { get; private set; }

    /// <summary>True after HLT or a fault. <see cref="Reset"/> or <see cref="LoadProgram(byte[], int)"/> clears it.</summary>
    public bool Halted { get; private set; }

    /// <summary>The fault that stopped the processor, or <see cref="FaultKind.None"/>.</summary>
    public FaultKind Fault { get; private set; }

    /// <summary>Message describing <see cref="Fault"/>.</summary>
    public string? FaultMessage { get; private set; }

    /// <summary>Trace of the most recently executed instruction.</summary>
    public StepTrace? LastStep { get; private set; }

    /// <summary>Raised after every instruction with the full trace of what it did.</summary>
    public event EventHandler<StepTrace>? InstructionExecuted;

    /// <summary>Raised for every micro-operation as it happens (bus transfers, register changes, ALU results ...).</summary>
    public event EventHandler<MicroOp>? MicroOperation;

    /// <summary>Raised when the OUT instruction writes a byte to a port.</summary>
    public event EventHandler<OutputEventArgs>? OutputWritten;

    /// <summary>Returns whether <paramref name="flag"/> is set.</summary>
    public bool IsFlagSet(Flag flag) => (flagRegister & (1 << (int)flag)) != 0;

    /// <summary>Sets or clears a single flag.</summary>
    public void SetFlag(Flag flag, bool value)
    {
        int bit = 1 << (int)flag;
        FlagRegister = (byte)(value ? flagRegister | bit : flagRegister & ~bit);
    }

    /// <summary>Returns R<paramref name="index"/>. Only the low three bits of the index are used, like a real register file decoder.</summary>
    public byte ReadRegister(int index) => registers[index & (RegisterCount - 1)];

    /// <summary>Stores <paramref name="value"/> into R<paramref name="index"/>.</summary>
    public void WriteRegister(int index, byte value)
    {
        index &= RegisterCount - 1;
        ChangeRegister($"R{index}", ref registers[index], value);
    }

    /// <summary>Returns the processor to its power-on state. Memory is kept unless <paramref name="clearMemory"/> is true.</summary>
    public void Reset(bool clearMemory = false)
    {
        accumulator = 0;
        Array.Clear(registers);
        instructionPointer = 0;
        stackPointer = InitialStackPointer;
        flagRegister = 0;
        instructionRegister = 0;
        memoryAddressRegister = 0;
        memoryDataRegister = 0;
        Cycles = 0;
        InstructionsExecuted = 0;
        Halted = false;
        Fault = FaultKind.None;
        FaultMessage = null;
        Phase = CyclePhase.Idle;
        LastStep = null;
        if (clearMemory) Memory.Clear();
    }

    /// <summary>Clears memory, copies <paramref name="image"/> to <paramref name="origin"/>, resets the registers and points IP at the program.</summary>
    public void LoadProgram(byte[] image, int origin = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        Reset(clearMemory: true);
        Memory.Load(image, origin);
        instructionPointer = (byte)origin;
    }

    /// <summary>Loads an assembled program (all of its segments) and points IP at its entry point.</summary>
    public void LoadProgram(AssembledProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        Reset(clearMemory: true);
        foreach (ListingLine line in program.Listing)
            Memory.Load(line.Bytes, line.Address);
        instructionPointer = (byte)program.EntryPoint;
    }

    /// <summary>Assembles <paramref name="source"/> and loads it.</summary>
    public AssembledProgram LoadProgram(string source)
    {
        AssembledProgram program = Assembler.Assemble(source);
        LoadProgram(program);
        return program;
    }

    /// <summary>Executes exactly one instruction (one full fetch - decode - execute cycle).</summary>
    /// <exception cref="InvalidOperationException">The processor is halted.</exception>
    public StepTrace Step()
    {
        if (Halted)
            throw new InvalidOperationException("The processor is halted. Reset it or load a new program before stepping.");

        var microOps = new List<MicroOp>(16);
        currentMicroOps = microOps;
        extraCycles = 0;
        int address = instructionPointer;
        byte opcode = 0;
        Operands operands = Operands.None;
        string disassembly = "?";
        int cycles = 0;

        try
        {
            Phase = CyclePhase.Fetch;
            opcode = ReadMemory(instructionPointer, MemoryAccess.Opcode);
            InstructionRegister = opcode;
            InstructionPointer = (byte)(instructionPointer + 1);

            Phase = CyclePhase.Decode;
            IInstruction? instruction = InstructionSet.Decode(opcode);
            if (instruction is null)
                RaiseFault(FaultKind.InvalidOpcode, $"Invalid opcode 0x{opcode:X2} at address 0x{address:X2}");

            operands = FetchOperands(instruction.Syntax.OperandByteCount);
            disassembly = instruction.Syntax.Format(operands);
            Record(MicroOp.Decode(opcode, instruction.Mnemonic, disassembly));

            Phase = CyclePhase.Execute;
            Record(MicroOp.Execute(instruction.Mnemonic));
            instruction.Execute(this, operands);

            cycles = instruction.Cycles + extraCycles;
            Cycles += cycles;
            InstructionsExecuted++;
            if (Halted) Phase = CyclePhase.Halted;
        }
        catch (ProcessorFaultException fault)
        {
            Halted = true;
            Fault = fault.Fault;
            FaultMessage = fault.Message;
            Phase = CyclePhase.Halted;
            Record(MicroOp.Fault(fault.Fault, fault.Message));
        }
        finally
        {
            currentMicroOps = null;
        }

        byte[] bytes = new byte[1 + operands.Count];
        bytes[0] = opcode;
        operands.ToArray().CopyTo(bytes, 1);

        var trace = new StepTrace(address, bytes, disassembly, cycles, microOps, GetState());
        LastStep = trace;
        InstructionExecuted?.Invoke(this, trace);
        return trace;
    }

    /// <summary>Runs until HLT, a fault, or <paramref name="maxInstructions"/> instructions have been executed.</summary>
    public RunResult Run(int maxInstructions = 100_000)
    {
        if (maxInstructions <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstructions), maxInstructions, "The instruction budget must be positive.");

        int executed = 0;
        while (!Halted && executed < maxInstructions)
        {
            Step();
            executed++;
        }

        StopReason reason = Fault != FaultKind.None ? StopReason.Fault
            : Halted ? StopReason.Halted
            : StopReason.InstructionLimit;
        return new RunResult(reason, InstructionsExecuted, Cycles, Fault, FaultMessage, GetState());
    }

    /// <summary>Takes an immutable snapshot of all registers.</summary>
    public ProcessorState GetState() => new(
        accumulator,
        (byte[])registers.Clone(),
        instructionPointer,
        stackPointer,
        flagRegister,
        instructionRegister,
        memoryAddressRegister,
        memoryDataRegister,
        Phase,
        Cycles,
        InstructionsExecuted,
        Halted,
        Fault);

    /// <summary>Reads a byte through the address and data registers (a bus cycle).</summary>
    public byte ReadMemory(int address, string purpose)
    {
        address = Memory.Wrap(address);
        MemoryAddressRegister = (byte)address;
        byte value = Memory.Read(address);
        Record(purpose == MemoryAccess.Opcode ? MicroOp.Fetch(address, value) : MicroOp.Read(address, value, purpose));
        MemoryDataRegister = value;
        return value;
    }

    /// <summary>Writes a byte through the address and data registers (a bus cycle).</summary>
    public void WriteMemory(int address, byte value, string purpose)
    {
        address = Memory.Wrap(address);
        MemoryAddressRegister = (byte)address;
        MemoryDataRegister = value;
        Memory.Write(address, value);
        Record(MicroOp.Write(address, value, purpose));
    }

    /// <summary>Pushes a byte: stores it at SP and decrements SP. Faults when the stack would wrap below 0x00.</summary>
    public void Push(byte value)
    {
        if (stackPointer == 0)
            RaiseFault(FaultKind.StackOverflow, "Stack overflow: SP is already 0x00");
        WriteMemory(stackPointer, value, MemoryAccess.Stack);
        StackPointer = (byte)(stackPointer - 1);
    }

    /// <summary>Pops a byte: increments SP and reads the byte at SP. Faults when the stack is empty.</summary>
    public byte Pop()
    {
        if (stackPointer == InitialStackPointer)
            RaiseFault(FaultKind.StackUnderflow, "Stack underflow: the stack is empty");
        StackPointer = (byte)(stackPointer + 1);
        return ReadMemory(stackPointer, MemoryAccess.Stack);
    }

    /// <summary>Transfers control to <paramref name="target"/> when <paramref name="taken"/>; always records the decision.</summary>
    public void Jump(string condition, byte target, bool taken)
    {
        Record(MicroOp.Jump(condition, target, taken));
        if (taken)
        {
            InstructionPointer = target;
            AddCycles(1);
        }
    }

    /// <summary>Writes a byte to an output port.</summary>
    public void Output(byte port, byte value)
    {
        Record(MicroOp.Output(port, value));
        OutputWritten?.Invoke(this, new OutputEventArgs(port, value));
    }

    /// <summary>Stops the processor (HLT).</summary>
    public void Halt()
    {
        Halted = true;
        Record(MicroOp.Halt());
    }

    /// <summary>Charges additional clock cycles to the current instruction (taken branches, long operations).</summary>
    public void AddCycles(int cycles) => extraCycles += cycles;

    /// <summary>Aborts the current instruction with a fault. Never returns.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public void RaiseFault(FaultKind fault, string message) =>
        throw new ProcessorFaultException(fault, LastInstructionAddress, message);

    /// <summary>Appends a micro-operation to the trace of the current instruction and raises <see cref="MicroOperation"/>.</summary>
    internal void Record(MicroOp op)
    {
        currentMicroOps?.Add(op);
        MicroOperation?.Invoke(this, op);
    }

    private int LastInstructionAddress => currentMicroOps?.Count > 0 && currentMicroOps[0].Kind == MicroOpKind.Fetch
        ? currentMicroOps[0].Address
        : instructionPointer;

    private Operands FetchOperands(int count)
    {
        if (count == 0) return Operands.None;
        byte first = ReadMemory(instructionPointer, MemoryAccess.Operand);
        InstructionPointer = (byte)(instructionPointer + 1);
        if (count == 1) return new Operands(1, first);
        byte second = ReadMemory(instructionPointer, MemoryAccess.Operand);
        InstructionPointer = (byte)(instructionPointer + 1);
        return new Operands(2, first, second);
    }

    private void ChangeRegister(string name, ref byte register, byte value)
    {
        if (register == value) return;
        byte before = register;
        register = value;
        Record(MicroOp.Register(name, before, value));
    }
}
