namespace CISCProcessorLibrary;

/// <summary>
/// Base class of every instruction. It owns the syntax template and implements the addressing modes,
/// so concrete instructions only describe *what* they do with the resolved operand values.
/// </summary>
public abstract class Instruction : IInstruction
{
    protected Instruction(byte opcode, string syntax, string description)
    {
        Opcode = opcode;
        Syntax = new InstructionSyntax(syntax);
        Description = description;
    }

    public byte Opcode { get; }

    public InstructionSyntax Syntax { get; }

    public string Mnemonic => Syntax.Mnemonic;

    public int Length => 1 + Syntax.OperandByteCount;

    public string Description { get; }

    public abstract void Execute(CISCProcessor cpu, Operands operands);

    /// <summary>Operand slot <paramref name="index"/> of the syntax template.</summary>
    protected OperandSlot Slot(int index) => Syntax.Operands[index];

    /// <summary>Reads the 8 bit value an operand slot refers to, performing the memory access of its addressing mode.</summary>
    protected static byte ReadOperand(CISCProcessor cpu, OperandSlot slot, Operands operands)
    {
        int o = slot.ByteOffset;
        return slot.Kind switch
        {
            OperandKind.Accumulator => cpu.Accumulator,
            OperandKind.Register => cpu.ReadRegister(operands[o]),
            OperandKind.Immediate => operands[o],
            OperandKind.Flags => cpu.FlagRegister,
            _ when slot.Kind.IsMemory() => cpu.ReadMemory(EffectiveAddress(cpu, slot, operands), MemoryAccess.Data),
            _ => throw new InvalidOperationException($"Operand kind {slot.Kind} is not an 8 bit operand."),
        };
    }

    /// <summary>Stores an 8 bit value into the register or memory cell an operand slot refers to.</summary>
    protected static void WriteOperand(CISCProcessor cpu, OperandSlot slot, Operands operands, byte value)
    {
        int o = slot.ByteOffset;
        switch (slot.Kind)
        {
            case OperandKind.Accumulator:
                cpu.Accumulator = value;
                break;
            case OperandKind.Register:
                cpu.WriteRegister(operands[o], value);
                break;
            case OperandKind.Flags:
                cpu.FlagRegister = value;
                break;
            default:
                if (!slot.Kind.IsMemory())
                    throw new InvalidOperationException($"Operand kind {slot.Kind} cannot be written as a byte.");
                cpu.WriteMemory(EffectiveAddress(cpu, slot, operands), value, MemoryAccess.Data);
                break;
        }
    }

    /// <summary>Reads the 16 bit value an operand slot refers to (address registers, imm16, or two bytes of memory).</summary>
    protected static ushort ReadOperand16(CISCProcessor cpu, OperandSlot slot, Operands operands)
    {
        int o = slot.ByteOffset;
        return slot.Kind switch
        {
            OperandKind.AddressRegister => cpu.ReadAddressRegister(operands[o]),
            OperandKind.Immediate16 => operands.Word(o),
            OperandKind.Address => operands.Word(o),
            _ when slot.Kind.IsMemory() => cpu.ReadMemoryWord(EffectiveAddress(cpu, slot, operands), MemoryAccess.Data),
            _ => throw new InvalidOperationException($"Operand kind {slot.Kind} is not a 16 bit operand."),
        };
    }

    /// <summary>Stores a 16 bit value into an address register or two bytes of memory.</summary>
    protected static void WriteOperand16(CISCProcessor cpu, OperandSlot slot, Operands operands, ushort value)
    {
        int o = slot.ByteOffset;
        switch (slot.Kind)
        {
            case OperandKind.AddressRegister:
                cpu.WriteAddressRegister(operands[o], value);
                break;
            default:
                if (!slot.Kind.IsMemory())
                    throw new InvalidOperationException($"Operand kind {slot.Kind} cannot be written as a word.");
                cpu.WriteMemoryWord(EffectiveAddress(cpu, slot, operands), value, MemoryAccess.Data);
                break;
        }
    }

    /// <summary>Computes the memory address an operand refers to, charging the timing model for the address calculation.</summary>
    protected static int EffectiveAddress(CISCProcessor cpu, OperandSlot slot, Operands operands)
    {
        int o = slot.ByteOffset;
        int address = slot.Kind switch
        {
            OperandKind.Direct => operands.Word(o),
            OperandKind.RegisterIndirect => cpu.ReadRegister(operands[o]),
            OperandKind.Indexed => operands.Word(o) + cpu.ReadRegister(operands[o + 2]),
            OperandKind.AddressRegisterIndirect => cpu.ReadAddressRegister(operands[o]),
            OperandKind.AddressRegisterDisplaced => cpu.ReadAddressRegister(operands[o]) + (sbyte)operands[o + 1],
            _ => throw new InvalidOperationException($"Operand kind {slot.Kind} does not address memory."),
        };
        cpu.ChargeEffectiveAddress(slot.Kind);
        return Memory.Wrap(address);
    }

    public override string ToString() => $"{Opcode:X2} {Syntax.Template}";
}
