namespace CISCProcessorLibrary;

/// <summary>An instruction decoded from memory.</summary>
public sealed record DisassembledInstruction(int Address, byte[] Bytes, string Text, IInstruction? Instruction)
{
    /// <summary>Size in bytes (1 for an invalid opcode).</summary>
    public int Length => Bytes.Length;

    /// <summary>The bytes as hex, e.g. "19 80 00 01".</summary>
    public string BytesText => string.Join(' ', Bytes.Select(b => b.ToString("X2")));
}

/// <summary>Turns bytes in memory back into assembly text.</summary>
public static class Disassembler
{
    /// <summary>Decodes the instruction that starts at <paramref name="address"/>.</summary>
    public static DisassembledInstruction Disassemble(Memory memory, int address) =>
        Disassemble(memory, address, InstructionSet.Default);

    /// <summary>Decodes the instruction that starts at <paramref name="address"/> using <paramref name="instructionSet"/>.</summary>
    public static DisassembledInstruction Disassemble(Memory memory, int address, InstructionSet instructionSet)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(instructionSet);

        address = Memory.Wrap(address);
        byte opcode = memory.Read(address);
        IInstruction? instruction = instructionSet.Decode(opcode);
        if (instruction is null)
            return new DisassembledInstruction(address, new[] { opcode }, $".db 0x{opcode:X2}", null);

        int count = instruction.Syntax.OperandByteCount;
        var bytes = new byte[1 + count];
        bytes[0] = opcode;
        for (int i = 0; i < count; i++)
            bytes[1 + i] = memory.Read(address + 1 + i);

        Operands operands = Operands.FromBytes(bytes.AsSpan(1));
        return new DisassembledInstruction(address, bytes, instruction.Syntax.Format(operands), instruction);
    }

    /// <summary>Decodes consecutive instructions starting at <paramref name="start"/> until <paramref name="count"/> bytes were covered.</summary>
    public static IEnumerable<DisassembledInstruction> DisassembleRange(Memory memory, int start, int count)
    {
        int address = start;
        int end = start + count;
        while (address < end && address < Memory.Size)
        {
            DisassembledInstruction item = Disassemble(memory, address);
            yield return item;
            address += item.Length;
        }
    }
}
