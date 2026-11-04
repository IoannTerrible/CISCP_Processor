using CISCProcessorLibrary.Instructions;

namespace CISCProcessorLibrary;

/// <summary>
/// The CISC-8 opcode table. Opcodes are grouped by family:
/// 0x0x control, 0x1x data movement, 0x2x stack, 0x3x-0x4x arithmetic, 0x5x logic,
/// 0x6x control flow, 0x7x flags, 0xFx input/output.
/// </summary>
public sealed class InstructionSet
{
    private readonly IInstruction?[] byOpcode = new IInstruction?[256];
    private readonly List<IInstruction> all = new();

    /// <summary>The standard CISC-8 instruction set.</summary>
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

        // 0x0x - control
        set.Add(new NopInstruction(0x00));
        set.Add(new HaltInstruction(0x01));

        // 0x1x - data movement
        set.Add(new MoveInstruction(0x10, "MOV A, imm8", 2, "Load an immediate into A"));
        set.Add(new MoveInstruction(0x11, "MOV Rn, imm8", 2, "Load an immediate into Rn"));
        set.Add(new MoveInstruction(0x12, "MOV A, Rn", 1, "Copy Rn to A"));
        set.Add(new MoveInstruction(0x13, "MOV Rn, A", 1, "Copy A to Rn"));
        set.Add(new MoveInstruction(0x14, "MOV Rn, Rm", 1, "Copy Rm to Rn"));
        set.Add(new MoveInstruction(0x15, "MOV A, [addr]", 3, "Load A from memory (direct)"));
        set.Add(new MoveInstruction(0x16, "MOV [addr], A", 3, "Store A to memory (direct)"));
        set.Add(new MoveInstruction(0x17, "MOV A, [Rn]", 3, "Load A from the address held in Rn (register indirect)"));
        set.Add(new MoveInstruction(0x18, "MOV [Rn], A", 3, "Store A to the address held in Rn (register indirect)"));
        set.Add(new MoveInstruction(0x19, "MOV A, [addr+Rn]", 4, "Load A from addr + Rn (indexed)"));
        set.Add(new MoveInstruction(0x1A, "MOV [addr+Rn], A", 4, "Store A to addr + Rn (indexed)"));
        set.Add(new MoveInstruction(0x1B, "MOV Rn, [addr]", 3, "Load Rn from memory (direct)"));
        set.Add(new MoveInstruction(0x1C, "MOV [addr], Rn", 3, "Store Rn to memory (direct)"));
        set.Add(new MoveInstruction(0x1D, "MOV SP, imm8", 2, "Set the stack pointer"));
        set.Add(new ExchangeInstruction(0x1E));

        // 0x2x - stack
        set.Add(new PushInstruction(0x20, "PUSH A", "Push A"));
        set.Add(new PushInstruction(0x21, "PUSH Rn", "Push Rn"));
        set.Add(new PushInstruction(0x22, "PUSH FLAGS", "Push the flags register"));
        set.Add(new PopInstruction(0x24, "POP A", "Pop into A"));
        set.Add(new PopInstruction(0x25, "POP Rn", "Pop into Rn"));
        set.Add(new PopInstruction(0x26, "POP FLAGS", "Pop into the flags register"));

        // 0x3x - arithmetic with a second operand
        set.Add(new AluInstruction(0x30, "ADD A, imm8", 2, AluOperation.Add, "A = A + imm8"));
        set.Add(new AluInstruction(0x31, "ADD A, Rn", 1, AluOperation.Add, "A = A + Rn"));
        set.Add(new AluInstruction(0x32, "ADD A, [addr]", 3, AluOperation.Add, "A = A + [addr]"));
        set.Add(new AluInstruction(0x33, "ADC A, imm8", 2, AluOperation.AddWithCarry, "A = A + imm8 + C"));
        set.Add(new AluInstruction(0x34, "ADC A, Rn", 1, AluOperation.AddWithCarry, "A = A + Rn + C"));
        set.Add(new AluInstruction(0x35, "ADC A, [addr]", 3, AluOperation.AddWithCarry, "A = A + [addr] + C"));
        set.Add(new AluInstruction(0x36, "SUB A, imm8", 2, AluOperation.Subtract, "A = A - imm8"));
        set.Add(new AluInstruction(0x37, "SUB A, Rn", 1, AluOperation.Subtract, "A = A - Rn"));
        set.Add(new AluInstruction(0x38, "SUB A, [addr]", 3, AluOperation.Subtract, "A = A - [addr]"));
        set.Add(new AluInstruction(0x39, "SBB A, imm8", 2, AluOperation.SubtractWithBorrow, "A = A - imm8 - C"));
        set.Add(new AluInstruction(0x3A, "SBB A, Rn", 1, AluOperation.SubtractWithBorrow, "A = A - Rn - C"));
        set.Add(new AluInstruction(0x3B, "SBB A, [addr]", 3, AluOperation.SubtractWithBorrow, "A = A - [addr] - C"));
        set.Add(new AluInstruction(0x3C, "CMP A, imm8", 2, AluOperation.Compare, "Set flags from A - imm8"));
        set.Add(new AluInstruction(0x3D, "CMP A, Rn", 1, AluOperation.Compare, "Set flags from A - Rn"));
        set.Add(new AluInstruction(0x3E, "CMP A, [addr]", 3, AluOperation.Compare, "Set flags from A - [addr]"));

        // 0x4x - single operand arithmetic
        set.Add(new UnaryInstruction(0x40, "INC A", 1, UnaryOperation.Increment, "A = A + 1 (C unchanged)"));
        set.Add(new UnaryInstruction(0x41, "INC Rn", 1, UnaryOperation.Increment, "Rn = Rn + 1 (C unchanged)"));
        set.Add(new UnaryInstruction(0x42, "INC [addr]", 4, UnaryOperation.Increment, "[addr] = [addr] + 1 (read-modify-write)"));
        set.Add(new UnaryInstruction(0x43, "DEC A", 1, UnaryOperation.Decrement, "A = A - 1 (C unchanged)"));
        set.Add(new UnaryInstruction(0x44, "DEC Rn", 1, UnaryOperation.Decrement, "Rn = Rn - 1 (C unchanged)"));
        set.Add(new UnaryInstruction(0x45, "DEC [addr]", 4, UnaryOperation.Decrement, "[addr] = [addr] - 1 (read-modify-write)"));
        set.Add(new MultiplyDivideInstruction(0x46, "MUL Rn", 8, MultiplyDivideOperation.Multiply, "A = low byte of A * Rn, C = V = overflow"));
        set.Add(new MultiplyDivideInstruction(0x47, "DIV Rn", 12, MultiplyDivideOperation.Divide, "A = A / Rn (unsigned), faults on zero"));
        set.Add(new MultiplyDivideInstruction(0x48, "MOD Rn", 12, MultiplyDivideOperation.Modulo, "A = A mod Rn (unsigned), faults on zero"));
        set.Add(new UnaryInstruction(0x49, "NEG A", 1, UnaryOperation.Negate, "A = -A (two's complement)"));

        // 0x5x - logic and shifts
        set.Add(new AluInstruction(0x50, "AND A, imm8", 2, AluOperation.And, "A = A and imm8"));
        set.Add(new AluInstruction(0x51, "AND A, Rn", 1, AluOperation.And, "A = A and Rn"));
        set.Add(new AluInstruction(0x52, "OR A, imm8", 2, AluOperation.Or, "A = A or imm8"));
        set.Add(new AluInstruction(0x53, "OR A, Rn", 1, AluOperation.Or, "A = A or Rn"));
        set.Add(new AluInstruction(0x54, "XOR A, imm8", 2, AluOperation.Xor, "A = A xor imm8"));
        set.Add(new AluInstruction(0x55, "XOR A, Rn", 1, AluOperation.Xor, "A = A xor Rn"));
        set.Add(new UnaryInstruction(0x56, "NOT A", 1, UnaryOperation.Not, "A = not A"));
        set.Add(new UnaryInstruction(0x57, "SHL A", 1, UnaryOperation.ShiftLeft, "Shift A left, C = old bit 7"));
        set.Add(new UnaryInstruction(0x58, "SHR A", 1, UnaryOperation.ShiftRight, "Shift A right, C = old bit 0"));
        set.Add(new UnaryInstruction(0x59, "ROL A", 1, UnaryOperation.RotateLeft, "Rotate A left, C = old bit 7"));
        set.Add(new UnaryInstruction(0x5A, "ROR A", 1, UnaryOperation.RotateRight, "Rotate A right, C = old bit 0"));

        // 0x6x - control flow
        set.Add(new JumpInstruction(0x60, "JMP addr", 2, JumpCondition.Always, "Jump"));
        set.Add(new JumpInstruction(0x61, "JZ addr", 2, JumpCondition.Zero, "Jump if Z = 1"));
        set.Add(new JumpInstruction(0x62, "JNZ addr", 2, JumpCondition.NotZero, "Jump if Z = 0"));
        set.Add(new JumpInstruction(0x63, "JC addr", 2, JumpCondition.Carry, "Jump if C = 1"));
        set.Add(new JumpInstruction(0x64, "JNC addr", 2, JumpCondition.NotCarry, "Jump if C = 0"));
        set.Add(new JumpInstruction(0x65, "JS addr", 2, JumpCondition.Sign, "Jump if N = 1"));
        set.Add(new JumpInstruction(0x66, "JNS addr", 2, JumpCondition.NotSign, "Jump if N = 0"));
        set.Add(new JumpInstruction(0x67, "JO addr", 2, JumpCondition.Overflow, "Jump if V = 1"));
        set.Add(new JumpInstruction(0x68, "JNO addr", 2, JumpCondition.NotOverflow, "Jump if V = 0"));
        set.Add(new CallInstruction(0x69));
        set.Add(new ReturnInstruction(0x6A));
        set.Add(new LoopInstruction(0x6B));

        // 0x7x - flags
        set.Add(new FlagInstruction(0x70, "CLC", Flag.Carry, false, "Clear the carry flag"));
        set.Add(new FlagInstruction(0x71, "STC", Flag.Carry, true, "Set the carry flag"));

        // 0xFx - input / output
        set.Add(new OutInstruction(0xF0));

        return set;
    }
}
