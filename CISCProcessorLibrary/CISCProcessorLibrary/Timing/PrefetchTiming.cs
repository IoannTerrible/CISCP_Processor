namespace CISCProcessorLibrary.Timing;

/// <summary>
/// 1985: the Intel 8086 split the processor into a bus interface unit (BIU) and an execution unit (EU).
/// The BIU owns the 16 bit bus: whenever the bus is free it reads the next instruction word (two bytes
/// in one 4 cycle bus cycle) into a 6 byte prefetch queue, while the EU decodes and executes whatever is
/// already queued. Sequential code therefore rarely waits for memory; a taken jump throws the queue away
/// and the EU stalls until the BIU has fetched the first word at the new address.
/// </summary>
public sealed class PrefetchTiming : TimingModel
{
    public const int QueueCapacity = 6;
    public const int BusCycles = 4;          // one bus cycle: a 16 bit word (or the odd byte of an unaligned fetch)
    public const int QueuedByteCycles = 1;   // the EU taking a byte out of the queue
    public const int DataCycles = 4;
    public const int PortCycles = 4;
    public const int EffectiveAddressCycles = 3;
    public const int AluCycles = 2;
    public const int WideAluCycles = 3;
    public const int MultiplyCycles = 30;
    public const int DivideCycles = 40;
    public const int TakenJumpCycles = 4;
    public const int InterruptCycles = 40;
    public const int IdleCycles = 4;

    private int queueHead;      // address of the first byte in the queue (the next one the EU will take)
    private int queueLength;    // bytes currently in the queue
    private int fetchAddress;   // address the BIU is fetching (or will fetch next)
    private int busProgress;    // cycles the BIU has already spent on the current fetch

    public PrefetchTiming(CISCProcessor cpu)
        : base(cpu)
    {
    }

    public override Microarchitecture Kind => Microarchitecture.Prefetch1985;

    public override string Name => "1985 prefetch queue (8086 style)";

    public override string Description =>
        "A bus interface unit keeps the 16 bit bus busy reading the next instruction words into a 6 byte queue " +
        "while the execution unit works, so straight line code never waits for memory. Taken jumps flush the queue.";

    public int QueueAddress => queueHead;

    public int QueueLength => queueLength;

    public override void Reset()
    {
        base.Reset();
        queueHead = 0;
        queueLength = 0;
        fetchAddress = 0;
        busProgress = 0;
    }

    public override TimingSnapshot Snapshot() =>
        new(Kind, Name, Statistics, queueHead, queueLength, QueueCapacity, Array.Empty<int>(), Array.Empty<int>(), 0, Array.Empty<int>());

    public override void OnOpcodeFetch(int address) => FetchByte(address);

    public override void OnOperandFetch(int address) => FetchByte(address);

    public override void OnDataRead(int address, string purpose) => BusBusy(DataCycles);

    public override void OnDataWrite(int address, string purpose) => BusBusy(DataCycles);

    public override void OnPortAccess(int port, bool input) => BusBusy(PortCycles);

    public override void OnEffectiveAddress(OperandKind kind)
    {
        if (kind is OperandKind.Indexed or OperandKind.AddressRegisterDisplaced)
            Internal(EffectiveAddressCycles);
    }

    public override void OnAlu(string operation, bool wide)
    {
        Count("ALU operations");
        Internal(operation switch
        {
            "MUL" => MultiplyCycles,
            "DIV" => DivideCycles,
            _ => wide ? WideAluCycles : AluCycles,
        });
    }

    public override void OnJump(int from, int target, bool taken, bool conditional, string kind)
    {
        Count(taken ? "branches taken" : "branches not taken");
        if (!taken) return;
        Count("queue flushes");
        Flush(target);
        Charge(0, "queue flush", $"taken {kind}: prefetch queue flushed, refetching from 0x{target:X4}", target);
        Internal(TakenJumpCycles);
    }

    public override void OnInstructionComplete(IInstruction instruction)
    {
    }

    public override void OnInterrupt()
    {
        Count("interrupts");
        Charge(InterruptCycles, "interrupt", "interrupt acknowledge, queue flushed");
        Flush(Cpu.InstructionPointer);
    }

    public override int OnIdle()
    {
        Internal(IdleCycles);
        return IdleCycles;
    }

    /// <summary>The EU takes one instruction byte: from the queue when it is there, otherwise it waits for the BIU.</summary>
    private void FetchByte(int address)
    {
        if (queueLength > 0 && address == queueHead)
        {
            Count("queue hits");
            queueLength--;
            queueHead = Memory.Wrap(queueHead + 1);
            Charge(QueuedByteCycles, "queue hit", $"byte 0x{address:X4} taken from the prefetch queue ({queueLength} left)", address);
            Internal(0);
            return;
        }

        if (address != fetchAddress || queueLength > 0)
            Flush(address);

        // The BIU is already fetching this very word; wait for the rest of that bus cycle.
        int wait = BusCycles - busProgress;
        Count("queue misses");
        Count("bus cycles");
        CompleteFetch();
        busProgress = 0;
        queueLength--;
        queueHead = Memory.Wrap(queueHead + 1);
        Charge(wait, "queue miss", $"byte 0x{address:X4}: waited {wait} cycles for the bus interface unit", address);
    }

    /// <summary>Cycles in which the EU does not need the bus: the BIU uses them to prefetch.</summary>
    private void Internal(int cycles)
    {
        if (cycles > 0) Charge(cycles);
        busProgress += cycles;
        while (busProgress >= BusCycles && queueLength < QueueCapacity)
        {
            busProgress -= BusCycles;
            Count("bus cycles");
            Count("words prefetched");
            CompleteFetch();
        }
        if (queueLength >= QueueCapacity) busProgress = 0;
        else if (busProgress >= BusCycles) busProgress = BusCycles - 1;
    }

    /// <summary>The EU occupies the bus itself (data or port transfer): the BIU pauses.</summary>
    private void BusBusy(int cycles)
    {
        Count("bus cycles");
        Charge(cycles);
    }

    /// <summary>Finishes the bus cycle the BIU was running: one word (two bytes) enters the queue.</summary>
    private void CompleteFetch()
    {
        int bytes = (fetchAddress & 1) != 0 ? 1 : 2;     // an unaligned fetch brings a single byte, like the 8086
        bytes = Math.Min(bytes, QueueCapacity - queueLength);
        if (bytes <= 0) bytes = 1;
        queueLength += bytes;
        fetchAddress = Memory.Wrap(fetchAddress + bytes);
    }

    private void Flush(int address)
    {
        queueLength = 0;
        queueHead = address;
        fetchAddress = address;
        busProgress = 0;
    }
}
