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

    /// <summary>The processor stopped (HLT instruction or a fault).</summary>
    Halted,
}
