using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CISCProcessorLibrary;

/// <summary>
/// Two pass assembler for CISC-8 assembly.
/// <para>Syntax: <c>[label:] MNEMONIC [operand[, operand]] [; comment]</c>.</para>
/// <para>Operands: <c>A</c>, <c>R0</c>..<c>R7</c>, <c>SP</c>, <c>FLAGS</c>, a number or label (immediate / address),
/// <c>[addr]</c>, <c>[Rn]</c> and <c>[addr+Rn]</c>. Numbers may be decimal, <c>0x</c> hex, <c>$</c> hex,
/// <c>0b</c> binary or a character literal such as <c>'A'</c>; <c>+</c> and <c>-</c> may combine them with labels.</para>
/// <para>Directives: <c>.org addr</c>, <c>.db byte, "text", ...</c>, <c>.ds count</c> and <c>.equ NAME, value</c>.</para>
/// </summary>
public static class Assembler
{
    private static readonly Regex LabelPattern = new(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*:(.*)$", RegexOptions.Compiled);
    private static readonly Regex RegisterPattern = new(@"^R([0-7])$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IdentifierPattern = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "SP", "FLAGS", "R0", "R1", "R2", "R3", "R4", "R5", "R6", "R7",
    };

    /// <summary>Assembles <paramref name="source"/> with the default instruction set.</summary>
    public static AssembledProgram Assemble(string source) => Assemble(source, InstructionSet.Default);

    /// <summary>Assembles <paramref name="source"/> against <paramref name="instructionSet"/>.</summary>
    public static AssembledProgram Assemble(string source, InstructionSet instructionSet)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(instructionSet);

        var symbols = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var statements = new List<Statement>();
        string[] lines = source.Replace("\r\n", "\n").Split('\n');

        // Pass 1: parse every line, resolve the instruction form, assign addresses and collect labels.
        int location = 0;
        int? entryPoint = null;
        foreach ((string rawLine, int index) in lines.Select((l, i) => (l, i)))
        {
            int lineNumber = index + 1;
            var statement = new Statement(lineNumber, rawLine, location);
            statements.Add(statement);

            string text = StripComment(rawLine).Trim();
            if (text.Length == 0) continue;

            Match label = LabelPattern.Match(text);
            if (label.Success)
            {
                DefineSymbol(symbols, label.Groups[1].Value, location, lineNumber);
                text = label.Groups[2].Value.Trim();
                if (text.Length == 0) continue;
            }

            if (text.StartsWith('.'))
            {
                ParseDirective(statement, text, symbols, ref location);
                continue;
            }

            ParseInstruction(statement, text, instructionSet);
            entryPoint ??= location;
            if (location + statement.Length > Memory.Size)
                throw new AssemblyException(lineNumber, $"Program does not fit in {Memory.Size} bytes of memory.");
            statement.Address = location;
            location += statement.Length;
        }

        // Pass 2: evaluate operands now that every label is known and emit the bytes.
        var occupied = new bool[Memory.Size];
        var listing = new List<ListingLine>(statements.Count);
        foreach (Statement statement in statements)
        {
            byte[] bytes = statement.Emit(symbols);
            for (int i = 0; i < bytes.Length; i++)
            {
                int address = statement.Address + i;
                if (address >= Memory.Size)
                    throw new AssemblyException(statement.LineNumber, $"Program does not fit in {Memory.Size} bytes of memory.");
                if (occupied[address])
                    throw new AssemblyException(statement.LineNumber, $"Address 0x{address:X2} is emitted twice (check your .org directives).");
                occupied[address] = true;
            }
            listing.Add(new ListingLine(statement.LineNumber, statement.Address, bytes, statement.Source, statement.Instruction is not null));
        }

        return new AssembledProgram(listing, symbols, entryPoint ?? 0);
    }

    /// <summary>Removes a trailing ; comment, ignoring semicolons inside quotes.</summary>
    private static string StripComment(string line)
    {
        char quote = '\0';
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == ';')
            {
                return line[..i];
            }
        }
        return line;
    }

    private static void DefineSymbol(Dictionary<string, int> symbols, string name, int value, int lineNumber)
    {
        if (ReservedWords.Contains(name))
            throw new AssemblyException(lineNumber, $"'{name}' is a register name and cannot be used as a label.");
        if (!symbols.TryAdd(name, value))
            throw new AssemblyException(lineNumber, $"Symbol '{name}' is defined more than once.");
    }

    private static void ParseDirective(Statement statement, string text, Dictionary<string, int> symbols, ref int location)
    {
        int line = statement.LineNumber;
        (string name, string arguments) = SplitHead(text);
        switch (name.ToUpperInvariant())
        {
            case ".ORG":
            {
                int address = EvaluateNow(arguments, symbols, line, ".org");
                if (address < 0 || address >= Memory.Size)
                    throw new AssemblyException(line, $".org address {address} is outside memory (0..{Memory.Size - 1}).");
                location = address;
                statement.Address = address;
                break;
            }
            case ".EQU":
            {
                string[] parts = SplitOperands(arguments);
                if (parts.Length != 2 || !IdentifierPattern.IsMatch(parts[0]))
                    throw new AssemblyException(line, ".equ expects a name and a value: .equ NAME, value");
                DefineSymbol(symbols, parts[0], EvaluateNow(parts[1], symbols, line, ".equ"), line);
                break;
            }
            case ".DB":
            {
                string[] items = SplitOperands(arguments);
                if (items.Length == 0)
                    throw new AssemblyException(line, ".db needs at least one value.");
                statement.DataItems = items;
                statement.Length = items.Sum(item => IsString(item) ? Unquote(item, line).Length : 1);
                if (location + statement.Length > Memory.Size)
                    throw new AssemblyException(line, $"Program does not fit in {Memory.Size} bytes of memory.");
                statement.Address = location;
                location += statement.Length;
                break;
            }
            case ".DS":
            {
                int count = EvaluateNow(arguments, symbols, line, ".ds");
                if (count < 0 || location + count > Memory.Size)
                    throw new AssemblyException(line, $".ds {count} does not fit in memory.");
                statement.ReservedBytes = count;
                statement.Length = count;
                statement.Address = location;
                location += count;
                break;
            }
            default:
                throw new AssemblyException(line, $"Unknown directive '{name}'. Supported: .org, .db, .ds, .equ");
        }
    }

    private static void ParseInstruction(Statement statement, string text, InstructionSet instructionSet)
    {
        int line = statement.LineNumber;
        (string mnemonic, string operandText) = SplitHead(text);
        string[] operandTexts = SplitOperands(operandText);
        ParsedOperand[] operands = operandTexts.Select(o => ParseOperand(o, line)).ToArray();

        IInstruction[] forms = instructionSet.WithMnemonic(mnemonic).ToArray();
        if (forms.Length == 0)
            throw new AssemblyException(line, $"Unknown mnemonic '{mnemonic.ToUpperInvariant()}'.");

        IInstruction? match = forms.FirstOrDefault(f => Matches(f.Syntax, operands));
        if (match is null)
        {
            string valid = string.Join(" | ", forms.Select(f => f.Syntax.Template));
            throw new AssemblyException(line, $"Invalid operands for {mnemonic.ToUpperInvariant()}. Valid forms: {valid}");
        }

        statement.Instruction = match;
        statement.Operands = operands;
        statement.Length = match.Length;
    }

    private static bool Matches(InstructionSyntax syntax, ParsedOperand[] operands)
    {
        if (syntax.Operands.Count != operands.Length) return false;
        for (int i = 0; i < operands.Length; i++)
        {
            OperandKind expected = syntax.Operands[i].Kind;
            OperandKind actual = operands[i].Kind;
            bool ok = expected == actual || (expected.IsNumeric() && actual.IsNumeric());
            if (!ok) return false;
        }
        return true;
    }

    private static ParsedOperand ParseOperand(string text, int line)
    {
        text = text.Trim();
        if (text.Length == 0)
            throw new AssemblyException(line, "Empty operand.");

        if (text.Equals("A", StringComparison.OrdinalIgnoreCase)) return new ParsedOperand(OperandKind.Accumulator);
        if (text.Equals("SP", StringComparison.OrdinalIgnoreCase)) return new ParsedOperand(OperandKind.StackPointer);
        if (text.Equals("FLAGS", StringComparison.OrdinalIgnoreCase)) return new ParsedOperand(OperandKind.Flags);

        Match register = RegisterPattern.Match(text);
        if (register.Success)
            return new ParsedOperand(OperandKind.Register) { Register = register.Groups[1].Value[0] - '0' };

        if (text.StartsWith('[') && text.EndsWith(']'))
        {
            string inner = text[1..^1].Trim();
            if (inner.Length == 0)
                throw new AssemblyException(line, "Empty memory operand [].");

            Match indirect = RegisterPattern.Match(inner);
            if (indirect.Success)
                return new ParsedOperand(OperandKind.RegisterIndirect) { Register = indirect.Groups[1].Value[0] - '0' };

            int plus = inner.LastIndexOf('+');
            if (plus > 0)
            {
                string left = inner[..plus].Trim();
                string right = inner[(plus + 1)..].Trim();
                Match rightRegister = RegisterPattern.Match(right);
                Match leftRegister = RegisterPattern.Match(left);
                if (rightRegister.Success)
                    return new ParsedOperand(OperandKind.Indexed) { Expression = left, Register = rightRegister.Groups[1].Value[0] - '0' };
                if (leftRegister.Success)
                    return new ParsedOperand(OperandKind.Indexed) { Expression = right, Register = leftRegister.Groups[1].Value[0] - '0' };
            }

            return new ParsedOperand(OperandKind.Direct) { Expression = inner };
        }

        if (text.Contains('[') || text.Contains(']'))
            throw new AssemblyException(line, $"Malformed memory operand '{text}'.");

        return new ParsedOperand(OperandKind.Immediate) { Expression = text };
    }

    /// <summary>Evaluates an expression that must already be known in pass 1 (.org, .equ, .ds).</summary>
    private static int EvaluateNow(string expression, Dictionary<string, int> symbols, int line, string directive)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new AssemblyException(line, $"{directive} needs a value.");
        return Evaluate(expression, symbols, line);
    }

    /// <summary>Evaluates "term ((+|-) term)*" where a term is a number, a character literal or a symbol.</summary>
    internal static int Evaluate(string expression, IReadOnlyDictionary<string, int> symbols, int line)
    {
        expression = expression.Trim();
        if (expression.Length == 0)
            throw new AssemblyException(line, "Empty expression.");

        int value = 0;
        int sign = 1;
        int i = 0;
        bool expectTerm = true;
        while (i < expression.Length)
        {
            char c = expression[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (expectTerm)
            {
                if (c == '-') { sign = -sign; i++; continue; }
                if (c == '+') { i++; continue; }

                int start = i;
                if (c == '\'')
                {
                    int close = expression.IndexOf('\'', i + 1);
                    if (close != i + 2)
                        throw new AssemblyException(line, $"Malformed character literal in '{expression}'.");
                    value += sign * expression[i + 1];
                    i = close + 1;
                }
                else
                {
                    while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] is '_' or '$'))
                        i++;
                    if (i == start)
                        throw new AssemblyException(line, $"Unexpected character '{c}' in expression '{expression}'.");
                    value += sign * EvaluateTerm(expression[start..i], symbols, line);
                }
                sign = 1;
                expectTerm = false;
            }
            else
            {
                if (c == '+') { sign = 1; }
                else if (c == '-') { sign = -1; }
                else throw new AssemblyException(line, $"Unexpected character '{c}' in expression '{expression}'.");
                i++;
                expectTerm = true;
            }
        }

        if (expectTerm)
            throw new AssemblyException(line, $"Expression '{expression}' ends with an operator.");
        return value;
    }

    private static int EvaluateTerm(string term, IReadOnlyDictionary<string, int> symbols, int line)
    {
        if (TryParseNumber(term, out int number))
            return number;
        if (IdentifierPattern.IsMatch(term))
        {
            if (symbols.TryGetValue(term, out int symbolValue))
                return symbolValue;
            throw new AssemblyException(line, $"Unknown symbol '{term}'.");
        }
        throw new AssemblyException(line, $"Invalid number '{term}'.");
    }

    /// <summary>Parses 123, 0x7F, $7F, 0b1010.</summary>
    internal static bool TryParseNumber(string text, out int value)
    {
        value = 0;
        if (text.Length == 0) return false;

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        if (text.StartsWith('$'))
            return int.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            string bits = text[2..];
            if (bits.Length == 0 || bits.Any(b => b is not ('0' or '1'))) return false;
            value = Convert.ToInt32(bits, 2);
            return true;
        }
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Converts an evaluated value into a byte, allowing negative two's complement values.</summary>
    internal static byte ToByte(int value, int line, string what)
    {
        if (value is < -128 or > 255)
            throw new AssemblyException(line, $"{what} {value} does not fit in one byte (-128..255).");
        return (byte)value;
    }

    private static (string Head, string Tail) SplitHead(string text)
    {
        int i = 0;
        while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
        return (text[..i], text[i..].Trim());
    }

    /// <summary>Splits on commas that are not inside brackets or quotes.</summary>
    private static string[] SplitOperands(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        int depth = 0;
        char quote = '\0';
        foreach (char c in text)
        {
            if (quote != '\0')
            {
                current.Append(c);
                if (c == quote) quote = '\0';
                continue;
            }
            switch (c)
            {
                case '"' or '\'':
                    quote = c;
                    current.Append(c);
                    break;
                case '[':
                    depth++;
                    current.Append(c);
                    break;
                case ']':
                    depth--;
                    current.Append(c);
                    break;
                case ',' when depth == 0:
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }
        if (current.ToString().Trim().Length > 0 || parts.Count > 0)
            parts.Add(current.ToString().Trim());
        return parts.Where(p => p.Length > 0 || parts.Count > 1).ToArray();
    }

    private static bool IsString(string item) => item.Length >= 2 && item[0] == '"' && item[^1] == '"';

    private static string Unquote(string item, int line)
    {
        if (!IsString(item))
            throw new AssemblyException(line, $"Malformed string {item}.");
        return item[1..^1];
    }

    private sealed class ParsedOperand
    {
        public ParsedOperand(OperandKind kind)
        {
            Kind = kind;
        }

        public OperandKind Kind { get; }

        public int Register { get; init; }

        public string? Expression { get; init; }
    }

    private sealed class Statement
    {
        public Statement(int lineNumber, string source, int address)
        {
            LineNumber = lineNumber;
            Source = source;
            Address = address;
        }

        public int LineNumber { get; }

        public string Source { get; }

        public int Address { get; set; }

        public int Length { get; set; }

        public IInstruction? Instruction { get; set; }

        public ParsedOperand[] Operands { get; set; } = Array.Empty<ParsedOperand>();

        public string[]? DataItems { get; set; }

        public int ReservedBytes { get; set; }

        public byte[] Emit(IReadOnlyDictionary<string, int> symbols)
        {
            if (Instruction is not null) return EmitInstruction(symbols);
            if (DataItems is not null) return EmitData(symbols);
            if (ReservedBytes > 0) return new byte[ReservedBytes];
            return Array.Empty<byte>();
        }

        private byte[] EmitInstruction(IReadOnlyDictionary<string, int> symbols)
        {
            IInstruction instruction = Instruction!;
            var bytes = new List<byte>(instruction.Length) { instruction.Opcode };
            for (int i = 0; i < instruction.Syntax.Operands.Count; i++)
            {
                OperandSlot slot = instruction.Syntax.Operands[i];
                ParsedOperand operand = Operands[i];
                switch (slot.Kind)
                {
                    case OperandKind.Register:
                    case OperandKind.RegisterIndirect:
                        bytes.Add((byte)operand.Register);
                        break;
                    case OperandKind.Immediate:
                    case OperandKind.Address:
                    case OperandKind.Direct:
                        bytes.Add(Assembler.ToByte(Evaluate(operand.Expression!, symbols, LineNumber), LineNumber, "Value"));
                        break;
                    case OperandKind.Indexed:
                        bytes.Add(Assembler.ToByte(Evaluate(operand.Expression!, symbols, LineNumber), LineNumber, "Address"));
                        bytes.Add((byte)operand.Register);
                        break;
                }
            }
            return bytes.ToArray();
        }

        private byte[] EmitData(IReadOnlyDictionary<string, int> symbols)
        {
            var bytes = new List<byte>(Length);
            foreach (string item in DataItems!)
            {
                if (IsString(item))
                {
                    foreach (char c in Unquote(item, LineNumber))
                    {
                        if (c > 0xFF)
                            throw new AssemblyException(LineNumber, $"Character '{c}' is not an 8 bit character.");
                        bytes.Add((byte)c);
                    }
                }
                else
                {
                    bytes.Add(Assembler.ToByte(Evaluate(item, symbols, LineNumber), LineNumber, "Value"));
                }
            }
            return bytes.ToArray();
        }
    }
}
