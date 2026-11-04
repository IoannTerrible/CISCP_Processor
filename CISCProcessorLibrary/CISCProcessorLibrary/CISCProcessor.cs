using CISCProcessorLibrary.Devices;
using CISCProcessorLibrary.Timing;

namespace CISCProcessorLibrary;

/// <summary>
/// The CISC-16 processor: an 8 bit data path (accumulator A and R0..R7) with a 16 bit address space,
/// the index and base registers SI, DI and BP, a hardware stack in memory, maskable interrupts with a
/// vector table, an I/O port bus with a timer and an input device, and a variable length CISC
/// instruction set. The control unit runs a classic fetch - decode - execute cycle and reports every
/// micro-operation it performs; a pluggable <see cref="TimingModel"/> decides how many clock cycles each
/// of them costs, which lets the same program run on a 1975, 1985 or 1995 style micro-architecture.
/// </summary>
public class CISCProcessor
{
    /// <summary>Number of general purpose 8 bit registers.</summary>
    public const int RegisterCount = 8;

    /// <summary>Number of 16 bit address registers (SI, DI, BP, SP).</summary>
    public const int AddressRegisterCount = 4;

    /// <summary>Encoding of SP among the address registers.</summary>
    public const int StackPointerIndex = 3;

    /// <summary>Value of SP after reset. The stack grows downwards from here; PUSH decrements first, then stores.</summary>
    public const ushort InitialStackPointer = 0xFFF0;

    /// <summary>Address of the interrupt vector table: eight little-endian handler addresses, IRQ n at + 2n.</summary>
    public const int InterruptVectorTable = 0xFFF0;

    /// <summary>Number of interrupt request lines.</summary>
    public const int InterruptCount = 8;

    private readonly byte[] registers = new byte[RegisterCount];
    private readonly ushort[] addressRegisters = new ushort[AddressRegisterCount];
    private readonly List<IDevice> devices = new();
    private byte accumulator;
    private ushort instructionPointer;
    private byte flagRegister;
    private byte instructionRegister;
    private ushort memoryAddressRegister;
    private byte memoryDataRegister;
    private byte pendingInterrupts;
    private ushort stackBase = InitialStackPointer;
    private int stepCycles;
    private int stepAddress;
    private List<MicroOp>? currentMicroOps;
    private TimingModel timing;

    public CISCProcessor()
        : this(Microarchitecture.Classic1975, InstructionSet.Default)
    {
    }

    public CISCProcessor(Microarchitecture microarchitecture)
        : this(microarchitecture, InstructionSet.Default)
    {
    }

    public CISCProcessor(Microarchitecture microarchitecture, InstructionSet instructionSet)
    {
        InstructionSet = instructionSet ?? throw new ArgumentNullException(nameof(instructionSet));
        Memory = new Memory();
        Ports = new PortBus();
        Alu = new ArithmeticLogicUnit(this);
        timing = TimingModel.Create(microarchitecture, this);
        Timer = new TimerDevice(this);
        InputDevice = new InputDevice(this);
        devices.Add(Timer);
        devices.Add(InputDevice);
        addressRegisters[StackPointerIndex] = InitialStackPointer;
    }

    /// <summary>The instruction set the decoder uses.</summary>
    public InstructionSet InstructionSet { get; }

    /// <summary>Main memory (64 KB shared by code, data, stack and the vector table).</summary>
    public Memory Memory { get; }

    /// <summary>The arithmetic logic unit.</summary>
    public ArithmeticLogicUnit Alu { get; }

    /// <summary>The I/O port bus the devices are attached to.</summary>
    public PortBus Ports { get; }

    /// <summary>The programmable interval timer on IRQ 0.</summary>
    public TimerDevice Timer { get; }

    /// <summary>The input device behind ports 0, 1 and 6.</summary>
    public InputDevice InputDevice { get; }

    /// <summary>Every attached device.</summary>
    public IReadOnlyList<IDevice> Devices => devices;

    /// <summary>The timing model in use (see <see cref="Microarchitecture"/>).</summary>
    public TimingModel Timing => timing;

    /// <summary>Which generation of micro-architecture the clock cycles are modelled after. Changing it resets the model.</summary>
    public Microarchitecture Microarchitecture
    {
        get => timing.Kind;
        set
        {
            if (value == timing.Kind) return;
            timing = TimingModel.Create(value, this);
        }
    }

    /// <summary>Accumulator A - the implicit operand of every 8 bit ALU instruction.</summary>
    public byte Accumulator
    {
        get => accumulator;
        set => ChangeRegister("A", ref accumulator, value);
    }

    /// <summary>Address of the next instruction to fetch.</summary>
    public ushort InstructionPointer
    {
        get => instructionPointer;
        set => ChangeRegister("IP", ref instructionPointer, value);
    }

    /// <summary>Top of the stack. PUSH decrements SP and stores; POP loads and increments.</summary>
    public ushort StackPointer
    {
        get => addressRegisters[StackPointerIndex];
        set => WriteAddressRegister(StackPointerIndex, value);
    }

    /// <summary>Base pointer: the anchor of the current stack frame (ENTER / LEAVE, [BP+disp8]).</summary>
    public ushort BasePointer
    {
        get => addressRegisters[2];
        set => WriteAddressRegister(2, value);
    }

    /// <summary>Source index register (string instructions read from [SI]).</summary>
    public ushort SourceIndex
    {
        get => addressRegisters[0];
        set => WriteAddressRegister(0, value);
    }

    /// <summary>Destination index register (string instructions write to [DI]).</summary>
    public ushort DestinationIndex
    {
        get => addressRegisters[1];
        set => WriteAddressRegister(1, value);
    }

    /// <summary>Status register. Bit positions are defined by <see cref="Flag"/>.</summary>
    public byte FlagRegister
    {
        get => flagRegister;
        set
        {
            byte masked = (byte)(value & 0x1F);
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
    public ushort MemoryAddressRegister
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

    /// <summary>Read-only view of SI, DI, BP, SP in encoding order.</summary>
    public IReadOnlyList<ushort> AddressRegisters => addressRegisters;

    /// <summary>Bit mask of interrupt requests waiting to be serviced.</summary>
    public byte PendingInterrupts => pendingInterrupts;

    /// <summary>Phase of the instruction cycle the control unit is in.</summary>
    public CyclePhase Phase { get; private set; } = CyclePhase.Idle;

    /// <summary>Clock cycles consumed since reset.</summary>
    public long Cycles { get; private set; }

    /// <summary>Instructions completed since reset (interrupt acknowledges and idle steps are not counted).</summary>
    public long InstructionsExecuted { get; private set; }

    /// <summary>True after HLT with interrupts disabled, or after a fault. <see cref="Reset"/> or <see cref="LoadProgram(byte[], int)"/> clears it.</summary>
    public bool Halted { get; private set; }

    /// <summary>True after HLT with interrupts enabled: the processor idles until an interrupt arrives.</summary>
    public bool Waiting { get; private set; }

    /// <summary>The fault that stopped the processor, or <see cref="FaultKind.None"/>.</summary>
    public FaultKind Fault { get; private set; }

    /// <summary>Message describing <see cref="Fault"/>.</summary>
    public string? FaultMessage { get; private set; }

    /// <summary>Trace of the most recent step.</summary>
    public StepTrace? LastStep { get; private set; }

    /// <summary>Raised after every step with the full trace of what it did.</summary>
    public event EventHandler<StepTrace>? InstructionExecuted;

    /// <summary>Raised for every micro-operation as it happens (bus transfers, register changes, ALU results ...).</summary>
    public event EventHandler<MicroOp>? MicroOperation;

    /// <summary>Raised when the OUT instruction writes a byte to a port no device claims (the console ports 0 and 1).</summary>
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

    /// <summary>Returns SI, DI, BP or SP (encodings 0..3).</summary>
    public ushort ReadAddressRegister(int index) => addressRegisters[index & (AddressRegisterCount - 1)];

    /// <summary>
    /// Stores a value into SI, DI, BP or SP. A program that loads SP this way moves the stack, so the
    /// new value also becomes the base used to detect stack underflow.
    /// </summary>
    public void WriteAddressRegister(int index, ushort value) => WriteAddressRegister(index, value, rebase: true);

    private void WriteAddressRegister(int index, ushort value, bool rebase)
    {
        index &= AddressRegisterCount - 1;
        if (index == StackPointerIndex && rebase) stackBase = value;
        ChangeRegister(InstructionSyntax.AddressRegisterName(index), ref addressRegisters[index], value);
    }

    /// <summary>Returns the processor to its power-on state. Memory is kept unless <paramref name="clearMemory"/> is true.</summary>
    public void Reset(bool clearMemory = false)
    {
        accumulator = 0;
        Array.Clear(registers);
        Array.Clear(addressRegisters);
        addressRegisters[StackPointerIndex] = InitialStackPointer;
        stackBase = InitialStackPointer;
        instructionPointer = 0;
        flagRegister = 0;
        instructionRegister = 0;
        memoryAddressRegister = 0;
        memoryDataRegister = 0;
        pendingInterrupts = 0;
        Cycles = 0;
        InstructionsExecuted = 0;
        Halted = false;
        Waiting = false;
        Fault = FaultKind.None;
        FaultMessage = null;
        Phase = CyclePhase.Idle;
        LastStep = null;
        timing.Reset();
        foreach (IDevice device in devices) device.Reset();
        if (clearMemory) Memory.Clear();
    }

    /// <summary>Clears memory, copies <paramref name="image"/> to <paramref name="origin"/>, resets the registers and points IP at the program.</summary>
    public void LoadProgram(byte[] image, int origin = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        Reset(clearMemory: true);
        Memory.Load(image, origin);
        instructionPointer = (ushort)origin;
    }

    /// <summary>Loads an assembled program (all of its segments) and points IP at its entry point.</summary>
    public void LoadProgram(AssembledProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        Reset(clearMemory: true);
        foreach (ListingLine line in program.Listing)
            Memory.Load(line.Bytes, line.Address);
        instructionPointer = (ushort)program.EntryPoint;
    }

    /// <summary>Assembles <paramref name="source"/> and loads it.</summary>
    public AssembledProgram LoadProgram(string source)
    {
        AssembledProgram program = Assembler.Assemble(source);
        LoadProgram(program);
        return program;
    }

    /// <summary>Raises interrupt request line <paramref name="irq"/> (0..7). It is serviced before the next instruction once I is set.</summary>
    public void RequestInterrupt(int irq)
    {
        if (irq is < 0 or >= InterruptCount)
            throw new ArgumentOutOfRangeException(nameof(irq), irq, $"IRQ lines are 0..{InterruptCount - 1}.");
        pendingInterrupts |= (byte)(1 << irq);
    }

    /// <summary>
    /// Performs one step: acknowledges a pending interrupt, idles one quantum while waiting for one,
    /// or fetches, decodes and executes the next instruction.
    /// </summary>
    /// <exception cref="InvalidOperationException">The processor is halted.</exception>
    public StepTrace Step()
    {
        if (Halted)
            throw new InvalidOperationException("The processor is halted. Reset it or load a new program before stepping.");

        var microOps = new List<MicroOp>(24);
        currentMicroOps = microOps;
        stepCycles = 0;
        stepAddress = instructionPointer;
        StepKind kind = StepKind.Instruction;
        byte opcode = 0;
        Operands operands = Operands.None;
        string disassembly = "?";

        try
        {
            int irq = NextPendingInterrupt();
            if (irq >= 0 && IsFlagSet(Flag.Interrupt))
            {
                kind = StepKind.Interrupt;
                Phase = CyclePhase.Interrupt;
                pendingInterrupts &= (byte)~(1 << irq);
                Waiting = false;
                EnterInterrupt(irq, software: false);
                disassembly = $"IRQ{irq} -> 0x{instructionPointer:X4}";
            }
            else if (Waiting)
            {
                kind = StepKind.Wait;
                Phase = CyclePhase.Waiting;
                int idle = timing.OnIdle();
                Record(MicroOp.Wait(idle));
                disassembly = "WAIT";
            }
            else
            {
                Phase = CyclePhase.Fetch;
                opcode = ReadMemory(instructionPointer, MemoryAccess.Opcode);
                InstructionRegister = opcode;
                InstructionPointer = (ushort)(instructionPointer + 1);

                Phase = CyclePhase.Decode;
                IInstruction? instruction = InstructionSet.Decode(opcode);
                if (instruction is null)
                    RaiseFault(FaultKind.InvalidOpcode, $"Invalid opcode 0x{opcode:X2} at address 0x{stepAddress:X4}");

                operands = FetchOperands(instruction.Syntax.OperandByteCount);
                disassembly = instruction.Syntax.Format(operands);
                Record(MicroOp.Decode(opcode, instruction.Mnemonic, disassembly));

                Phase = CyclePhase.Execute;
                Record(MicroOp.Execute(instruction.Mnemonic));
                instruction.Execute(this, operands);
                timing.OnInstructionComplete(instruction);
                InstructionsExecuted++;
                if (Halted) Phase = CyclePhase.Halted;
                else if (Waiting) Phase = CyclePhase.Waiting;
            }
        }
        catch (ProcessorFaultException fault)
        {
            Halted = true;
            Waiting = false;
            Fault = fault.Fault;
            FaultMessage = fault.Message;
            Phase = CyclePhase.Halted;
            Record(MicroOp.Fault(fault.Fault, fault.Message));
        }
        finally
        {
            currentMicroOps = null;
        }

        int cycles = stepCycles;
        Cycles += cycles;
        foreach (IDevice device in devices) device.Tick(cycles);

        byte[] bytes = kind == StepKind.Instruction ? Combine(opcode, operands) : Array.Empty<byte>();
        var trace = new StepTrace(kind, stepAddress, bytes, disassembly, cycles, microOps, GetState());
        LastStep = trace;
        InstructionExecuted?.Invoke(this, trace);
        return trace;
    }

    /// <summary>Runs until HLT, a fault, or <paramref name="maxSteps"/> steps (instructions, interrupts and idle quanta) have been performed.</summary>
    public RunResult Run(int maxSteps = 100_000)
    {
        if (maxSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSteps), maxSteps, "The step budget must be positive.");

        int steps = 0;
        while (!Halted && steps < maxSteps)
        {
            Step();
            steps++;
        }

        StopReason reason = Fault != FaultKind.None ? StopReason.Fault
            : Halted ? StopReason.Halted
            : StopReason.StepLimit;
        return new RunResult(reason, InstructionsExecuted, Cycles, Fault, FaultMessage, GetState());
    }

    /// <summary>Takes an immutable snapshot of all registers.</summary>
    public ProcessorState GetState() => new(
        accumulator,
        (byte[])registers.Clone(),
        instructionPointer,
        addressRegisters[StackPointerIndex],
        addressRegisters[2],
        addressRegisters[0],
        addressRegisters[1],
        flagRegister,
        instructionRegister,
        memoryAddressRegister,
        memoryDataRegister,
        pendingInterrupts,
        Phase,
        Cycles,
        InstructionsExecuted,
        Halted,
        Waiting,
        Fault);

    /// <summary>Reads a byte through the address and data registers (one bus cycle), charging the timing model.</summary>
    public byte ReadMemory(int address, string purpose)
    {
        address = Memory.Wrap(address);
        switch (purpose)
        {
            case MemoryAccess.Opcode: timing.OnOpcodeFetch(address); break;
            case MemoryAccess.Operand: timing.OnOperandFetch(address); break;
            default: timing.OnDataRead(address, purpose); break;
        }
        MemoryAddressRegister = (ushort)address;
        byte value = Memory.Read(address);
        Record(purpose == MemoryAccess.Opcode ? MicroOp.Fetch(address, value) : MicroOp.Read(address, value, purpose));
        MemoryDataRegister = value;
        return value;
    }

    /// <summary>Writes a byte through the address and data registers (one bus cycle), charging the timing model.</summary>
    public void WriteMemory(int address, byte value, string purpose)
    {
        address = Memory.Wrap(address);
        timing.OnDataWrite(address, purpose);
        MemoryAddressRegister = (ushort)address;
        MemoryDataRegister = value;
        Memory.Write(address, value);
        Record(MicroOp.Write(address, value, purpose));
    }

    /// <summary>Reads a little-endian word as two bus cycles.</summary>
    public ushort ReadMemoryWord(int address, string purpose)
    {
        byte low = ReadMemory(address, purpose);
        byte high = ReadMemory(address + 1, purpose);
        return (ushort)(low | (high << 8));
    }

    /// <summary>Writes a little-endian word as two bus cycles.</summary>
    public void WriteMemoryWord(int address, ushort value, string purpose)
    {
        WriteMemory(address, (byte)value, purpose);
        WriteMemory(address + 1, (byte)(value >> 8), purpose);
    }

    /// <summary>Pushes a byte: decrements SP and stores at the new SP. Faults when the stack would wrap below 0x0000.</summary>
    public void Push(byte value)
    {
        ushort sp = StackPointer;
        if (sp < 1)
            RaiseFault(FaultKind.StackOverflow, "Stack overflow: SP is already 0x0000");
        WriteAddressRegister(StackPointerIndex, (ushort)(sp - 1), rebase: false);
        WriteMemory(sp - 1, value, MemoryAccess.Stack);
    }

    /// <summary>Pops a byte: reads at SP and increments SP. Faults when the stack is empty.</summary>
    public byte Pop()
    {
        ushort sp = StackPointer;
        if (sp >= stackBase)
            RaiseFault(FaultKind.StackUnderflow, "Stack underflow: the stack is empty");
        byte value = ReadMemory(sp, MemoryAccess.Stack);
        WriteAddressRegister(StackPointerIndex, (ushort)(sp + 1), rebase: false);
        return value;
    }

    /// <summary>Pushes a 16 bit value (low byte at the lower address).</summary>
    public void PushWord(ushort value)
    {
        ushort sp = StackPointer;
        if (sp < 2)
            RaiseFault(FaultKind.StackOverflow, "Stack overflow: no room for a 16 bit push");
        WriteAddressRegister(StackPointerIndex, (ushort)(sp - 2), rebase: false);
        WriteMemoryWord(sp - 2, value, MemoryAccess.Stack);
    }

    /// <summary>Pops a 16 bit value.</summary>
    public ushort PopWord()
    {
        ushort sp = StackPointer;
        if (sp + 2 > stackBase)
            RaiseFault(FaultKind.StackUnderflow, "Stack underflow: no 16 bit value on the stack");
        ushort value = ReadMemoryWord(sp, MemoryAccess.Stack);
        WriteAddressRegister(StackPointerIndex, (ushort)(sp + 2), rebase: false);
        return value;
    }

    /// <summary>Moves SP by <paramref name="delta"/> without moving the stack base (ENTER reserving locals, LEAVE).</summary>
    public void AdjustStackPointer(int delta)
    {
        int sp = StackPointer + delta;
        if (sp < 0)
            RaiseFault(FaultKind.StackOverflow, "Stack overflow: the stack frame does not fit below 0x0000");
        if (sp > stackBase)
            RaiseFault(FaultKind.StackUnderflow, "Stack underflow: SP moved above the stack base");
        WriteAddressRegister(StackPointerIndex, (ushort)sp, rebase: false);
    }

    /// <summary>Transfers control to <paramref name="target"/> when <paramref name="taken"/>; always records the decision and informs the timing model.</summary>
    public void Jump(string condition, ushort target, bool taken, bool conditional, string kind)
    {
        Record(MicroOp.Jump(condition, target, taken));
        timing.OnJump(stepAddress, target, taken, conditional, kind);
        if (taken)
            InstructionPointer = target;
    }

    /// <summary>Writes a byte to an output port. Ports no device claims raise <see cref="OutputWritten"/>.</summary>
    public void Output(byte port, byte value)
    {
        timing.OnPortAccess(port, input: false);
        Record(MicroOp.Output(port, value));
        if (!Ports.Write(port, value))
            OutputWritten?.Invoke(this, new OutputEventArgs(port, value));
    }

    /// <summary>Reads a byte from an input port (0xFF when nothing drives the port).</summary>
    public byte Input(byte port)
    {
        timing.OnPortAccess(port, input: true);
        byte value = Ports.Read(port);
        Record(MicroOp.Input(port, value));
        return value;
    }

    /// <summary>
    /// Acknowledges interrupt <paramref name="irq"/>: reads the vector, pushes FLAGS and IP, clears I and
    /// jumps to the handler. Used for hardware requests and for the INT instruction.
    /// </summary>
    public void EnterInterrupt(int irq, bool software)
    {
        int vector = InterruptVectorTable + (irq & (InterruptCount - 1)) * 2;
        ushort handler = ReadMemoryWord(vector, MemoryAccess.Vector);
        Record(MicroOp.Interrupt(irq, handler));
        Push(flagRegister);
        PushWord(instructionPointer);
        SetFlag(Flag.Interrupt, false);
        Waiting = false;
        timing.OnInterrupt();
        Jump(software ? "INT" : $"IRQ{irq}", handler, taken: true, conditional: false, kind: "interrupt");
    }

    /// <summary>IRET: pops IP and FLAGS pushed by the interrupt entry.</summary>
    public void ReturnFromInterrupt()
    {
        ushort target = PopWord();
        FlagRegister = Pop();
        Jump("IRET", target, taken: true, conditional: false, kind: "ret");
    }

    /// <summary>HLT: stops the clock, or waits for the next interrupt when they are enabled.</summary>
    public void Halt()
    {
        if (IsFlagSet(Flag.Interrupt))
        {
            Waiting = true;
            Record(new MicroOp { Kind = MicroOpKind.Halt, Name = "HLT", Text = "waiting for an interrupt" });
        }
        else
        {
            Halted = true;
            Record(MicroOp.Halt());
        }
    }

    /// <summary>Charges clock cycles to the current step (called by the timing model).</summary>
    public void AddCycles(int cycles) => stepCycles += cycles;

    /// <summary>Tells the timing model that the ALU performed an operation.</summary>
    public void ChargeAlu(string operation, bool wide) => timing.OnAlu(operation, wide);

    /// <summary>Tells the timing model that an effective address was computed.</summary>
    public void ChargeEffectiveAddress(OperandKind kind) => timing.OnEffectiveAddress(kind);

    /// <summary>Aborts the current step with a fault. Never returns.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public void RaiseFault(FaultKind fault, string message) =>
        throw new ProcessorFaultException(fault, stepAddress, message);

    /// <summary>Appends a micro-operation to the trace of the current step and raises <see cref="MicroOperation"/>.</summary>
    public void Record(MicroOp op)
    {
        currentMicroOps?.Add(op);
        MicroOperation?.Invoke(this, op);
    }

    private int NextPendingInterrupt()
    {
        if (pendingInterrupts == 0) return -1;
        for (int irq = 0; irq < InterruptCount; irq++)
            if ((pendingInterrupts & (1 << irq)) != 0) return irq;
        return -1;
    }

    private Operands FetchOperands(int count)
    {
        Span<byte> bytes = stackalloc byte[Operands.MaxBytes];
        for (int i = 0; i < count; i++)
        {
            bytes[i] = ReadMemory(instructionPointer, MemoryAccess.Operand);
            InstructionPointer = (ushort)(instructionPointer + 1);
        }
        return Operands.FromBytes(bytes[..count]);
    }

    private static byte[] Combine(byte opcode, Operands operands)
    {
        var bytes = new byte[1 + operands.Count];
        bytes[0] = opcode;
        operands.ToArray().CopyTo(bytes, 1);
        return bytes;
    }

    private void ChangeRegister(string name, ref byte register, byte value)
    {
        if (register == value) return;
        byte before = register;
        register = value;
        Record(MicroOp.Register(name, before, value));
    }

    private void ChangeRegister(string name, ref ushort register, ushort value)
    {
        if (register == value) return;
        ushort before = register;
        register = value;
        Record(MicroOp.Register(name, before, value));
    }
}
