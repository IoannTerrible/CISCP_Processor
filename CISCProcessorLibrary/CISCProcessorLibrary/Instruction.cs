namespace CISCProcessorLibrary;

/// <summary>
/// Base class of every instruction. It owns the syntax template and implements the addressing modes,
/// so concrete instructions only describe *what* they do with the resolved operand values.
/// </summary>
public abstract class Instruction : IInstruction
{
    protected Instruction(byte opcode, string syntax, int cycles, string description)
    {
        Opcode = opcode;
        Syntax = new InstructionSyntax(syntax);
        Cycles = cycles;
        Description = description;
    }

    public byte Opcode { get; }

    public InstructionSyntax Syntax { get; }

    public string Mnemonic => Syntax.Mnemonic;

    public int Cycles { get; }

    public int Length => 1 + Syntax.OperandByteCount;

    public string Description { get; }

    public abstract void Execute(CISCProcessor cpu, Operands operands);

    /// <summary>Operand slot <paramref name="index"/> of the syntax template.</summary>
    protected OperandSlot Slot(int index) => Syntax.Operands[index];

    /// <summary>Reads the value an operand slot refers to, performing the memory access of its addressing mode.</summary>
    protected static byte ReadOperand(CISCProcessor cpu, OperandSlot slot, Operands operands) => slot.Kind switch
    {
        OperandKind.Accumulator => cpu.Accumulator,
        OperandKind.Register => cpu.ReadRegister(operands[slot.ByteOffset]),
        OperandKind.Immediate => operands[slot.ByteOffset],
        OperandKind.Address => operands[slot.ByteOffset],
        OperandKind.Direct => cpu.ReadMemory(operands[slot.ByteOffset], MemoryAccess.Data),
        OperandKind.RegisterIndirect => cpu.ReadMemory(cpu.ReadRegister(operands[slot.ByteOffset]), MemoryAccess.Data),
        OperandKind.Indexed => cpu.ReadMemory(EffectiveAddress(cpu, slot, operands), MemoryAccess.Data),
        OperandKind.StackPointer => cpu.StackPointer,
        OperandKind.Flags => cpu.FlagRegister,
        _ => throw new InvalidOperationException($"Unsupported operand kind {slot.Kind}."),
    };

    /// <summary>Stores <paramref name="value"/> into the register or memory cell an operand slot refers to.</summary>
    protected static void WriteOperand(CISCProcessor cpu, OperandSlot slot, Operands operands, byte value)
    {
        switch (slot.Kind)
        {
            case OperandKind.Accumulator:
                cpu.Accumulator = value;
                break;
            case OperandKind.Register:
                cpu.WriteRegister(operands[slot.ByteOffset], value);
                break;
            case OperandKind.Direct:
                cpu.WriteMemory(operands[slot.ByteOffset], value, MemoryAccess.Data);
                break;
            case OperandKind.RegisterIndirect:
                cpu.WriteMemory(cpu.ReadRegister(operands[slot.ByteOffset]), value, MemoryAccess.Data);
                break;
            case OperandKind.Indexed:
                cpu.WriteMemory(EffectiveAddress(cpu, slot, operands), value, MemoryAccess.Data);
                break;
            case OperandKind.StackPointer:
                cpu.StackPointer = value;
                break;
            case OperandKind.Flags:
                cpu.FlagRegister = value;
                break;
            default:
                throw new InvalidOperationException($"Operand kind {slot.Kind} cannot be written.");
        }
    }

    /// <summary>Computes addr + Rn for the indexed addressing mode (wrapping inside the 8 bit address space).</summary>
    private static int EffectiveAddress(CISCProcessor cpu, OperandSlot slot, Operands operands) =>
        Memory.Wrap(operands[slot.ByteOffset] + cpu.ReadRegister(operands[slot.ByteOffset + 1]));

    public override string ToString() => $"{Opcode:X2} {Syntax.Template}";
}
