using System.Text;
using System.Text.RegularExpressions;

namespace CISCProcessorLibrary;

/// <summary>
/// One operand slot of an instruction: its addressing mode and where its bytes live inside the operand stream.
/// </summary>
public sealed record OperandSlot(OperandKind Kind, int ByteOffset, string? Literal = null)
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
    /// <summary>Names of the 16 bit address registers in encoding order.</summary>
    public static readonly string[] AddressRegisterNames = { "SI", "DI", "BP", "SP" };

    private static readonly Regex LiteralPattern = new("^[A-Z][A-Z0-9]*$", RegexOptions.Compiled);
    private static readonly int MaxOperandBytes = global::CISCProcessorLibrary.Operands.MaxBytes;

    private static readonly Dictionary<string, OperandKind> Tokens = new(StringComparer.Ordinal)
    {
        ["A"] = OperandKind.Accumulator,
        ["Rn"] = OperandKind.Register,
        ["Rm"] = OperandKind.Register,
        ["XR"] = OperandKind.AddressRegister,
        ["XS"] = OperandKind.AddressRegister,
        ["imm8"] = OperandKind.Immediate,
        ["port"] = OperandKind.Immediate,
        ["imm16"] = OperandKind.Immediate16,
        ["addr"] = OperandKind.Address,
        ["[addr]"] = OperandKind.Direct,
        ["[Rn]"] = OperandKind.RegisterIndirect,
        ["[addr+Rn]"] = OperandKind.Indexed,
        ["[XR]"] = OperandKind.AddressRegisterIndirect,
        ["[XS]"] = OperandKind.AddressRegisterIndirect,
        ["[XR+disp8]"] = OperandKind.AddressRegisterDisplaced,
        ["[XS+disp8]"] = OperandKind.AddressRegisterDisplaced,
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
                if (Tokens.TryGetValue(token, out OperandKind kind))
                    slots.Add(new OperandSlot(kind, offset));
                else if (LiteralPattern.IsMatch(token))
                    slots.Add(new OperandSlot(OperandKind.Literal, offset, token));
                else
                    throw new ArgumentException($"Unknown operand token '{token}' in syntax template '{template}'.", nameof(template));
                offset += slots[^1].ByteCount;
            }
        }

        if (offset > MaxOperandBytes)
            throw new ArgumentException($"Syntax template '{template}' needs {offset} operand bytes but at most {MaxOperandBytes} are supported.", nameof(template));

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

    /// <summary>Name of the address register with the given encoding (only the low two bits are used).</summary>
    public static string AddressRegisterName(int code) => AddressRegisterNames[code & 3];

    /// <summary>Renders the instruction with concrete operand values, e.g. "MOV A, [0x0080+R1]".</summary>
    public string Format(Operands operands)
    {
        if (Operands.Count == 0)
            return Mnemonic;

        var text = new StringBuilder(Mnemonic).Append(' ');
        for (int i = 0; i < Operands.Count; i++)
        {
            if (i > 0) text.Append(", ");
            text.Append(FormatOperand(Operands[i], operands));
        }
        return text.ToString();
    }

    private static string FormatOperand(OperandSlot slot, Operands operands)
    {
        int o = slot.ByteOffset;
        return slot.Kind switch
        {
            OperandKind.Accumulator => "A",
            OperandKind.Flags => "FLAGS",
            OperandKind.Literal => slot.Literal ?? "?",
            OperandKind.Register => $"R{operands[o] & 7}",
            OperandKind.AddressRegister => AddressRegisterName(operands[o]),
            OperandKind.Immediate => operands[o].ToString(),
            OperandKind.Immediate16 => $"0x{operands.Word(o):X4}",
            OperandKind.Address => $"0x{operands.Word(o):X4}",
            OperandKind.Direct => $"[0x{operands.Word(o):X4}]",
            OperandKind.RegisterIndirect => $"[R{operands[o] & 7}]",
            OperandKind.Indexed => $"[0x{operands.Word(o):X4}+R{operands[o + 2] & 7}]",
            OperandKind.AddressRegisterIndirect => $"[{AddressRegisterName(operands[o])}]",
            OperandKind.AddressRegisterDisplaced => FormatDisplaced(operands[o], (sbyte)operands[o + 1]),
            _ => "?",
        };
    }

    private static string FormatDisplaced(int register, int displacement) =>
        displacement >= 0 ? $"[{AddressRegisterName(register)}+{displacement}]" : $"[{AddressRegisterName(register)}{displacement}]";

    public override string ToString() => Template;
}
