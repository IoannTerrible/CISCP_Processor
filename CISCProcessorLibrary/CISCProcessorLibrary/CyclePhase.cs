namespace CISCProcessorLibrary;

/// <summary>Phase of the instruction cycle the control unit is currently in.</summary>
public enum CyclePhase
{
    /// <summary>Power-on or reset state - nothing has been fetched yet.</summary>
    Idle,

    /// <summary>The opcode byte addressed by IP is being read from memory into IR.</summary>
    Fetch,

    /// <summary>The opcode is being looked up in the instruction set and operand bytes are being read.</summary>
    Decode,

    /// <summary>The decoded instruction is being executed by the ALU / data path.</summary>
    Execute,

    /// <summary>An interrupt request is being acknowledged: FLAGS and IP are saved and the vector is loaded.</summary>
    Interrupt,

    /// <summary>HLT was executed with interrupts enabled: the clock runs but nothing executes until an interrupt arrives.</summary>
    Waiting,

    /// <summary>The processor stopped (HLT with interrupts disabled, or a fault).</summary>
    Halted,
}
