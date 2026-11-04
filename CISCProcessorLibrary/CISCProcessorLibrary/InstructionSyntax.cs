using System.Text;

namespace CISCProcessorLibrary;

/// <summary>
/// One operand slot of an instruction: its addressing mode and where its bytes live inside the operand stream.
/// </summary>
public sealed record OperandSlot(OperandKind Kind, int ByteOffset)
{
    /// <summary>Number of bytes the slot occupies.</summary>
    public int ByteCount => Kind.ByteCount();
}

/// <summary>
/// Parses the assembly syntax template of an instruction ("MOV A, [addr+Rn]") into operand slots.
/// The template is the single source of truth for three things: how the assembler recognises the
/// instruction, in which order operand bytes are encoded, and how the disassembler prints it back.
/// </summary>
public sealed class InstructionSyntax
{
    private static readonly Dictionary<string, OperandKind> Tokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = OperandKind.Accumulator,
        ["Rn"] = OperandKind.Register,
        ["Rm"] = OperandKind.Register,
        ["imm8"] = OperandKind.Immediate,
        ["port"] = OperandKind.Immediate,
        ["addr"] = OperandKind.Address,
        ["[addr]"] = OperandKind.Direct,
        ["[Rn]"] = OperandKind.RegisterIndirect,
        ["[addr+Rn]"] = OperandKind.Indexed,
        ["SP"] = OperandKind.StackPointer,
        ["FLAGS"] = OperandKind.Flags,
    };

    public InstructionSyntax(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template.Trim();

        int space = Template.IndexOf(' ');
        Mnemonic = (space < 0 ? Template : Template[..space]).ToUpperInvariant();

        var slots = new List<OperandSlot>();
        int offset = 0;
        if (space >= 0)
        {
            foreach (string raw in Template[(space + 1)..].Split(','))
            {
                string token = raw.Trim();
                if (!Tokens.TryGetValue(token, out OperandKind kind))
                    throw new ArgumentException($"Unknown operand token '{token}' in syntax template '{template}'.", nameof(template));
                slots.Add(new OperandSlot(kind, offset));
                offset += kind.ByteCount();
            }
        }

        if (offset > 2)
            throw new ArgumentException($"Syntax template '{template}' needs {offset} operand bytes but at most two are supported.", nameof(template));

        Operands = slots;
        OperandByteCount = offset;
    }

    /// <summary>The template exactly as written, for example "MOV A, [addr+Rn]".</summary>
    public string Template { get; }

    /// <summary>Upper-case mnemonic, for example "MOV".</summary>
    public string Mnemonic { get; }

    /// <summary>Operand slots in source order.</summary>
    public IReadOnlyList<OperandSlot> Operands { get; }

    /// <summary>Total number of operand bytes that follow the opcode.</summary>
    public int OperandByteCount { get; }

    /// <summary>Renders the instruction with concrete operand values, e.g. "MOV A, [0x80+R1]".</summary>
    public string Format(Operands operands)
    {
        if (Operands.Count == 0)
            return Mnemonic;

        var text = new StringBuilder(Mnemonic).Append(' ');
        for (int i = 0; i < Operands.Count; i++)
        {
            if (i > 0) text.Append(", ");
            OperandSlot slot = Operands[i];
            text.Append(slot.Kind switch
            {
                OperandKind.Accumulator => "A",
                OperandKind.StackPointer => "SP",
                OperandKind.Flags => "FLAGS",
                OperandKind.Register => $"R{operands[slot.ByteOffset] & 7}",
                OperandKind.Immediate => operands[slot.ByteOffset].ToString(),
                OperandKind.Address => $"0x{operands[slot.ByteOffset]:X2}",
                OperandKind.Direct => $"[0x{operands[slot.ByteOffset]:X2}]",
                OperandKind.RegisterIndirect => $"[R{operands[slot.ByteOffset] & 7}]",
                OperandKind.Indexed => $"[0x{operands[slot.ByteOffset]:X2}+R{operands[slot.ByteOffset + 1] & 7}]",
                _ => "?",
            });
        }
        return text.ToString();
    }

    public override string ToString() => Template;
}
