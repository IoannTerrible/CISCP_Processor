using CISCProcessorLibrary.Instructions;

namespace CISCProcessorLibrary;

/// <summary>
/// The CISC-16 opcode table. Opcodes are grouped by family:
/// 0x0x control and interrupts, 0x1x-0x2x data movement, 0x3x stack and frames, 0x4x-0x5x arithmetic,
/// 0x6x logic and shifts, 0x7x control flow, 0x8x flags, 0x9x string operations, 0xFx input / output.
/// </summary>
public sealed class InstructionSet
{
    private readonly IInstruction?[] byOpcode = new IInstruction?[256];
    private readonly List<IInstruction> all = new();

    /// <summary>The standard CISC-16 instruction set.</summary>
    public static InstructionSet Default { get; } = CreateDefault();

    /// <summary>Every instruction in opcode order.</summary>
    public IReadOnlyList<IInstruction> All => all;

    /// <summary>Returns the instruction encoded by <paramref name="opcode"/>, or null for an invalid opcode.</summary>
    public IInstruction? Decode(byte opcode) => byOpcode[opcode];

    /// <summary>Every form of the given mnemonic (case-insensitive), e.g. all four ADD variants.</summary>
    public IEnumerable<IInstruction> WithMnemonic(string mnemonic) =>
        all.Where(i => string.Equals(i.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));

    /// <summary>Distinct mnemonics in opcode order.</summary>
    public IEnumerable<string> Mnemonics => all.Select(i => i.Mnemonic).Distinct();

    /// <summary>Adds an instruction to the table. Each opcode can be defined only once.</summary>
    public void Add(IInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        if (byOpcode[instruction.Opcode] is not null)
            throw new InvalidOperationException($"Opcode 0x{instruction.Opcode:X2} is already defined.");
        byOpcode[instruction.Opcode] = instruction;
        all.Add(instruction);
        all.Sort((x, y) => x.Opcode.CompareTo(y.Opcode));
    }

    private static InstructionSet CreateDefault()
    {
        var set = new InstructionSet();

        // 0x0x - control and interrupts
        set.Add(new NopInstruction(0x00));
        set.Add(new HaltInstruction(0x01));
        set.Add(new InterruptFlagInstruction(0x02, "EI", true, "Enable maskable interrupts (set I)"));
        set.Add(new InterruptFlagInstruction(0x03, "DI", false, "Disable maskable interrupts (clear I)"));
        set.Add(new InterruptReturnInstruction(0x04));
        set.Add(new SoftwareInterruptInstruction(0x05));

        // 0x1x - 8 bit data movement
        set.Add(new MoveInstruction(0x10, "MOV A, imm8", "Load an immediate into A"));
        set.Add(new MoveInstruction(0x11, "MOV Rn, imm8", "Load an immediate into Rn"));
        set.Add(new MoveInstruction(0x12, "MOV A, Rn", "Copy Rn to A"));
        set.Add(new MoveInstruction(0x13, "MOV Rn, A", "Copy A to Rn"));
        set.Add(new MoveInstruction(0x14, "MOV Rn, Rm", "Copy Rm to Rn"));
        set.Add(new MoveInstruction(0x15, "MOV A, [addr]", "Load A from memory (direct, 16 bit address)"));
        set.Add(new MoveInstruction(0x16, "MOV [addr], A", "Store A to memory (direct)"));
        set.Add(new MoveInstruction(0x17, "MOV A, [Rn]", "Load A from zero page address Rn (register indirect)"));
        set.Add(new MoveInstruction(0x18, "MOV [Rn], A", "Store A to zero page address Rn (register indirect)"));
        set.Add(new MoveInstruction(0x19, "MOV A, [addr+Rn]", "Load A from addr + Rn (indexed)"));
        set.Add(new MoveInstruction(0x1A, "MOV [addr+Rn], A", "Store A to addr + Rn (indexed)"));
        set.Add(new MoveInstruction(0x1B, "MOV Rn, [addr]", "Load Rn from memory (direct)"));
        set.Add(new MoveInstruction(0x1C, "MOV [addr], Rn", "Store Rn to memory (direct)"));
        set.Add(new MoveInstruction(0x1D, "MOV A, [XR]", "Load A from the address in SI / DI / BP / SP"));
        set.Add(new MoveInstruction(0x1E, "MOV [XR], A", "Store A to the address in SI / DI / BP / SP"));
        set.Add(new MoveInstruction(0x1F, "MOV A, [XR+disp8]", "Load A from XR + signed displacement (based)"));

        // 0x2x - more data movement, 16 bit
        set.Add(new MoveInstruction(0x20, "MOV [XR+disp8], A", "Store A to XR + signed displacement (based)"));
        set.Add(new ExchangeInstruction(0x21));
        set.Add(new MoveInstruction(0x22, "MOV XR, imm16", "Load a 16 bit immediate into SI / DI / BP / SP"));
        set.Add(new MoveInstruction(0x23, "MOV XR, XS", "Copy one address register to another"));
        set.Add(new MoveInstruction(0x24, "MOV XR, [addr]", "Load a 16 bit word (little-endian) into an address register"));
        set.Add(new MoveInstruction(0x25, "MOV [addr], XR", "Store an address register as a 16 bit word"));
        set.Add(new LoadEffectiveAddressInstruction(0x26));
        set.Add(new MoveInstruction(0x27, "MOV Rn, [XR+disp8]", "Load Rn from XR + signed displacement (based)"));
        set.Add(new MoveInstruction(0x28, "MOV [XR+disp8], Rn", "Store Rn to XR + signed displacement (based)"));
        set.Add(new AddressArithmeticInstruction(0x29, "ADD XR, A", AddressOperation.Add, "XR = XR + A (zero extended), for indexing tables"));

        // 0x3x - stack and stack frames
        set.Add(new PushInstruction(0x30, "PUSH A", "Push A"));
        set.Add(new PushInstruction(0x31, "PUSH Rn", "Push Rn"));
        set.Add(new PushInstruction(0x32, "PUSH FLAGS", "Push the flags register"));
        set.Add(new PushInstruction(0x33, "PUSH XR", "Push an address register (16 bit)"));
        set.Add(new PopInstruction(0x34, "POP A", "Pop into A"));
        set.Add(new PopInstruction(0x35, "POP Rn", "Pop into Rn"));
        set.Add(new PopInstruction(0x36, "POP FLAGS", "Pop into the flags register"));
        set.Add(new PopInstruction(0x37, "POP XR", "Pop into an address register (16 bit)"));
        set.Add(new EnterInstruction(0x38));
        set.Add(new LeaveInstruction(0x39));

        // 0x4x - 8 bit arithmetic with a second operand
        set.Add(new AluInstruction(0x40, "ADD A, imm8", AluOperation.Add, "A = A + imm8"));
        set.Add(new AluInstruction(0x41, "ADD A, Rn", AluOperation.Add, "A = A + Rn"));
        set.Add(new AluInstruction(0x42, "ADD A, [addr]", AluOperation.Add, "A = A + [addr]"));
        set.Add(new AluInstruction(0x43, "ADD A, [XR]", AluOperation.Add, "A = A + [XR]"));
        set.Add(new AluInstruction(0x44, "ADC A, imm8", AluOperation.AddWithCarry, "A = A + imm8 + C"));
        set.Add(new AluInstruction(0x45, "ADC A, Rn", AluOperation.AddWithCarry, "A = A + Rn + C"));
        set.Add(new AluInstruction(0x46, "ADC A, [addr]", AluOperation.AddWithCarry, "A = A + [addr] + C"));
        set.Add(new AluInstruction(0x47, "SUB A, imm8", AluOperation.Subtract, "A = A - imm8"));
        set.Add(new AluInstruction(0x48, "SUB A, Rn", AluOperation.Subtract, "A = A - Rn"));
        set.Add(new AluInstruction(0x49, "SUB A, [addr]", AluOperation.Subtract, "A = A - [addr]"));
        set.Add(new AluInstruction(0x4A, "SUB A, [XR]", AluOperation.Subtract, "A = A - [XR]"));
        set.Add(new AluInstruction(0x4B, "SBB A, imm8", AluOperation.SubtractWithBorrow, "A = A - imm8 - C"));
        set.Add(new AluInstruction(0x4C, "SBB A, Rn", AluOperation.SubtractWithBorrow, "A = A - Rn - C"));
        set.Add(new AluInstruction(0x4D, "SBB A, [addr]", AluOperation.SubtractWithBorrow, "A = A - [addr] - C"));
        set.Add(new AluInstruction(0x4E, "CMP A, imm8", AluOperation.Compare, "Set flags from A - imm8"));
        set.Add(new AluInstruction(0x4F, "CMP A, Rn", AluOperation.Compare, "Set flags from A - Rn"));

        // 0x5x - more arithmetic
        set.Add(new AluInstruction(0x50, "CMP A, [addr]", AluOperation.Compare, "Set flags from A - [addr]"));
        set.Add(new AluInstruction(0x51, "CMP A, [XR]", AluOperation.Compare, "Set flags from A - [XR]"));
        set.Add(new UnaryInstruction(0x52, "INC A", UnaryOperation.Increment, "A = A + 1 (C unchanged)"));
        set.Add(new UnaryInstruction(0x53, "INC Rn", UnaryOperation.Increment, "Rn = Rn + 1 (C unchanged)"));
        set.Add(new UnaryInstruction(0x54, "INC [addr]", UnaryOperation.Increment, "[addr] = [addr] + 1 (read-modify-write)"));
        set.Add(new UnaryInstruction(0x55, "DEC A", UnaryOperation.Decrement, "A = A - 1 (C unchanged)"));
        set.Add(new UnaryInstruction(0x56, "DEC Rn", UnaryOperation.Decrement, "Rn = Rn - 1 (C unchanged)"));
        set.Add(new UnaryInstruction(0x57, "DEC [addr]", UnaryOperation.Decrement, "[addr] = [addr] - 1 (read-modify-write)"));
        set.Add(new MultiplyInstruction(0x58));
        set.Add(new DivideInstruction(0x59));
        set.Add(new UnaryInstruction(0x5A, "NEG A", UnaryOperation.Negate, "A = -A (two's complement)"));
        set.Add(new AddressArithmeticInstruction(0x5B, "ADD XR, imm16", AddressOperation.Add, "XR = XR + imm16 (16 bit)"));
        set.Add(new AddressArithmeticInstruction(0x5C, "SUB XR, imm16", AddressOperation.Subtract, "XR = XR - imm16 (16 bit)"));
        set.Add(new AddressArithmeticInstruction(0x5D, "CMP XR, imm16", AddressOperation.Compare, "Set flags from XR - imm16 (16 bit)"));
        set.Add(new AddressArithmeticInstruction(0x5E, "INC XR", AddressOperation.Increment, "XR = XR + 1 (C unchanged)"));
        set.Add(new AddressArithmeticInstruction(0x5F, "DEC XR", AddressOperation.Decrement, "XR = XR - 1 (C unchanged)"));

        // 0x6x - logic and shifts
        set.Add(new AluInstruction(0x60, "AND A, imm8", AluOperation.And, "A = A and imm8"));
        set.Add(new AluInstruction(0x61, "AND A, Rn", AluOperation.And, "A = A and Rn"));
        set.Add(new AluInstruction(0x62, "OR A, imm8", AluOperation.Or, "A = A or imm8"));
        set.Add(new AluInstruction(0x63, "OR A, Rn", AluOperation.Or, "A = A or Rn"));
        set.Add(new AluInstruction(0x64, "XOR A, imm8", AluOperation.Xor, "A = A xor imm8"));
        set.Add(new AluInstruction(0x65, "XOR A, Rn", AluOperation.Xor, "A = A xor Rn"));
        set.Add(new UnaryInstruction(0x66, "NOT A", UnaryOperation.Not, "A = not A"));
        set.Add(new UnaryInstruction(0x67, "SHL A", UnaryOperation.ShiftLeft, "Shift A left, C = old bit 7"));
        set.Add(new UnaryInstruction(0x68, "SHR A", UnaryOperation.ShiftRight, "Shift A right, C = old bit 0"));
        set.Add(new UnaryInstruction(0x69, "ROL A", UnaryOperation.RotateLeft, "Rotate A left, C = old bit 7"));
        set.Add(new UnaryInstruction(0x6A, "ROR A", UnaryOperation.RotateRight, "Rotate A right, C = old bit 0"));

        // 0x7x - control flow
        set.Add(new JumpInstruction(0x70, "JMP addr", JumpCondition.Always, "Jump"));
        set.Add(new JumpInstruction(0x71, "JZ addr", JumpCondition.Zero, "Jump if Z = 1"));
        set.Add(new JumpInstruction(0x72, "JNZ addr", JumpCondition.NotZero, "Jump if Z = 0"));
        set.Add(new JumpInstruction(0x73, "JC addr", JumpCondition.Carry, "Jump if C = 1"));
        set.Add(new JumpInstruction(0x74, "JNC addr", JumpCondition.NotCarry, "Jump if C = 0"));
        set.Add(new JumpInstruction(0x75, "JS addr", JumpCondition.Sign, "Jump if N = 1"));
        set.Add(new JumpInstruction(0x76, "JNS addr", JumpCondition.NotSign, "Jump if N = 0"));
        set.Add(new JumpInstruction(0x77, "JO addr", JumpCondition.Overflow, "Jump if V = 1"));
        set.Add(new JumpInstruction(0x78, "JNO addr", JumpCondition.NotOverflow, "Jump if V = 0"));
        set.Add(new CallInstruction(0x79));
        set.Add(new ReturnInstruction(0x7A));
        set.Add(new LoopInstruction(0x7B));
        set.Add(new IndirectJumpInstruction(0x7C));

        // 0x8x - flags
        set.Add(new FlagInstruction(0x80, "CLC", Flag.Carry, false, "Clear the carry flag"));
        set.Add(new FlagInstruction(0x81, "STC", Flag.Carry, true, "Set the carry flag"));

        // 0x9x - string operations (SI = source, DI = destination, R6 = count)
        set.Add(new StringInstruction(0x90, "LODSB", StringOperation.Load, false, "A = [SI], SI = SI + 1"));
        set.Add(new StringInstruction(0x91, "STOSB", StringOperation.Store, false, "[DI] = A, DI = DI + 1"));
        set.Add(new StringInstruction(0x92, "MOVSB", StringOperation.Move, false, "[DI] = [SI], SI and DI advance"));
        set.Add(new StringInstruction(0x93, "CMPSB", StringOperation.Compare, false, "Set flags from [SI] - [DI], SI and DI advance"));
        set.Add(new StringInstruction(0x94, "SCASB", StringOperation.Scan, false, "Set flags from A - [DI], DI advances"));
        set.Add(new StringInstruction(0x95, "REP MOVSB", StringOperation.Move, true, "Copy R6 bytes from [SI] to [DI]"));
        set.Add(new StringInstruction(0x96, "REP STOSB", StringOperation.Store, true, "Fill R6 bytes at [DI] with A"));
        set.Add(new StringInstruction(0x97, "REP CMPSB", StringOperation.Compare, true, "Compare up to R6 bytes, stop at the first difference (REPE)"));
        set.Add(new StringInstruction(0x98, "REP SCASB", StringOperation.Scan, true, "Scan up to R6 bytes at [DI] for A, stop at the first match (REPNE)"));

        // 0xFx - input / output
        set.Add(new OutInstruction(0xF0));
        set.Add(new InInstruction(0xF1));

        return set;
    }
}
