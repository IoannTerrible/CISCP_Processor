using CISCProcessorLibrary;
using Xunit;
using static CISCProcessorLibrary.Tests.TestHelpers;

namespace CISCProcessorLibrary.Tests;

public class AssemblerTests
{
    [Theory]
    [InlineData("NOP", new byte[] { 0x00 })]
    [InlineData("HLT", new byte[] { 0x01 })]
    [InlineData("MOV A, 42", new byte[] { 0x10, 42 })]
    [InlineData("MOV R3, 0x7F", new byte[] { 0x11, 3, 0x7F })]
    [InlineData("MOV A, R5", new byte[] { 0x12, 5 })]
    [InlineData("MOV R6, A", new byte[] { 0x13, 6 })]
    [InlineData("MOV R2, R5", new byte[] { 0x14, 2, 5 })]
    [InlineData("MOV A, [0x1234]", new byte[] { 0x15, 0x34, 0x12 })]
    [InlineData("MOV [0x80], A", new byte[] { 0x16, 0x80, 0x00 })]
    [InlineData("MOV A, [R1]", new byte[] { 0x17, 1 })]
    [InlineData("MOV [R7], A", new byte[] { 0x18, 7 })]
    [InlineData("MOV A, [0x80+R1]", new byte[] { 0x19, 0x80, 0x00, 1 })]
    [InlineData("MOV A, [R1+0x1234]", new byte[] { 0x19, 0x34, 0x12, 1 })]
    [InlineData("MOV [0x40+R2], A", new byte[] { 0x1A, 0x40, 0x00, 2 })]
    [InlineData("MOV R4, [0x20]", new byte[] { 0x1B, 4, 0x20, 0x00 })]
    [InlineData("MOV [0x20], R3", new byte[] { 0x1C, 0x20, 0x00, 3 })]
    [InlineData("MOV A, [SI]", new byte[] { 0x1D, 0 })]
    [InlineData("MOV [DI], A", new byte[] { 0x1E, 1 })]
    [InlineData("MOV A, [BP+4]", new byte[] { 0x1F, 2, 4 })]
    [InlineData("MOV A, [BP-1]", new byte[] { 0x1F, 2, 0xFF })]
    [InlineData("MOV [SP+2], A", new byte[] { 0x20, 3, 2 })]
    [InlineData("XCHG A, R1", new byte[] { 0x21, 1 })]
    [InlineData("MOV SI, 0x1234", new byte[] { 0x22, 0, 0x34, 0x12 })]
    [InlineData("MOV SP, 0xFF00", new byte[] { 0x22, 3, 0x00, 0xFF })]
    [InlineData("MOV DI, SI", new byte[] { 0x23, 1, 0 })]
    [InlineData("MOV BP, [0x0100]", new byte[] { 0x24, 2, 0x00, 0x01 })]
    [InlineData("MOV [0x0100], SP", new byte[] { 0x25, 0x00, 0x01, 3 })]
    [InlineData("LEA DI, [SI+8]", new byte[] { 0x26, 1, 0, 8 })]
    [InlineData("MOV R1, [BP-2]", new byte[] { 0x27, 1, 2, 0xFE })]
    [InlineData("ADD SI, A", new byte[] { 0x29, 0 })]
    [InlineData("PUSH FLAGS", new byte[] { 0x32 })]
    [InlineData("PUSH SI", new byte[] { 0x33, 0 })]
    [InlineData("POP R2", new byte[] { 0x35, 2 })]
    [InlineData("ENTER 4", new byte[] { 0x38, 4 })]
    [InlineData("LEAVE", new byte[] { 0x39 })]
    [InlineData("ADD A, [0x10]", new byte[] { 0x42, 0x10, 0x00 })]
    [InlineData("ADD A, [DI]", new byte[] { 0x43, 1 })]
    [InlineData("CMP A, R0", new byte[] { 0x4F, 0 })]
    [InlineData("INC [0x33]", new byte[] { 0x54, 0x33, 0x00 })]
    [InlineData("MUL R2", new byte[] { 0x58, 2 })]
    [InlineData("ADD SI, 0x0100", new byte[] { 0x5B, 0, 0x00, 0x01 })]
    [InlineData("INC DI", new byte[] { 0x5E, 1 })]
    [InlineData("JNZ 0x0110", new byte[] { 0x72, 0x10, 0x01 })]
    [InlineData("CALL 0x20", new byte[] { 0x79, 0x20, 0x00 })]
    [InlineData("LOOP R0, 0x0105", new byte[] { 0x7B, 0, 0x05, 0x01 })]
    [InlineData("JMP [SI]", new byte[] { 0x7C, 0 })]
    [InlineData("LODSB", new byte[] { 0x90 })]
    [InlineData("REP MOVSB", new byte[] { 0x95 })]
    [InlineData("rep stosb", new byte[] { 0x96 })]
    [InlineData("REP SCASB", new byte[] { 0x98 })]
    [InlineData("INT 3", new byte[] { 0x05, 3 })]
    [InlineData("IRET", new byte[] { 0x04 })]
    [InlineData("OUT 1, A", new byte[] { 0xF0, 1 })]
    [InlineData("IN A, 6", new byte[] { 0xF1, 6 })]
    public void Encodes_EveryAddressingMode(string source, byte[] expected)
    {
        Assert.Equal(expected, Bytes(source));
    }

    [Fact]
    public void Mnemonics_AndRegisters_AreCaseInsensitive()
    {
        Assert.Equal(new byte[] { 0x19, 0x80, 0x00, 1 }, Bytes("mov a, [0x80+r1]"));
        Assert.Equal(new byte[] { 0x1F, 2, 4 }, Bytes("mov a, [bp+4]"));
    }

    [Theory]
    [InlineData("0x1F", 0x1F)]
    [InlineData("$1F", 0x1F)]
    [InlineData("0b1010", 10)]
    [InlineData("'A'", 65)]
    [InlineData("255", 255)]
    [InlineData("-1", 0xFF)]
    [InlineData("-128", 0x80)]
    [InlineData("10+5", 15)]
    [InlineData("20 - 4", 16)]
    public void Numbers_SupportSeveralFormats(string literal, int expected)
    {
        Assert.Equal(new byte[] { 0x10, (byte)expected }, Bytes($"MOV A, {literal}"));
    }

    [Fact]
    public void Labels_CanBeReferencedBeforeTheyAreDefined()
    {
        byte[] bytes = Bytes("""
                JMP end
                NOP
            end: HLT
            """);

        Assert.Equal(new byte[] { 0x70, 0x04, 0x00, 0x00, 0x01 }, bytes);
    }

    [Fact]
    public void Labels_WorkInExpressionsAndIndexedOperands()
    {
        AssembledProgram program = Assembler.Assemble("""
                MOV A, [table+1]
                MOV A, [table+R2]
                MOV SI, table
                HLT
            table: .db 1, 2, 3
            """);

        int table = program.Symbols["table"];
        Assert.Equal(12, table);
        Assert.Equal(new byte[] { 0x15, (byte)(table + 1), 0 }, program.Listing[0].Bytes);
        Assert.Equal(new byte[] { 0x19, (byte)table, 0, 2 }, program.Listing[1].Bytes);
        Assert.Equal(new byte[] { 0x22, 0, (byte)table, 0 }, program.Listing[2].Bytes);
    }

    [Fact]
    public void Directives_OrgDbDwDsEqu()
    {
        AssembledProgram program = Assembler.Assemble("""
                .equ START, 0x20
                .equ LIMIT, START + 2
                .org START
                MOV A, LIMIT
            buffer: .ds 3
            text:   .db "Hi;", 0, 'x'
            words:  .dw 0x1234, text
                HLT
            """);

        Assert.Equal(0x20, program.EntryPoint);
        Assert.Equal(0x22, program.Symbols["LIMIT"]);
        Assert.Equal(0x22, program.Symbols["buffer"]);
        Assert.Equal(0x25, program.Symbols["text"]);
        Assert.Equal(0x2A, program.Symbols["words"]);
        byte[] image = program.ToImage();
        Assert.Equal(new byte[] { 0x10, 0x22 }, image[0x20..0x22]);
        Assert.Equal(new byte[] { 0, 0, 0 }, image[0x22..0x25]);
        Assert.Equal(new byte[] { (byte)'H', (byte)'i', (byte)';', 0, (byte)'x' }, image[0x25..0x2A]);
        Assert.Equal(new byte[] { 0x34, 0x12, 0x25, 0x00, 0x01 }, image[0x2A..0x2F]);
        Assert.Equal(2 + 3 + 5 + 4 + 1, program.Length);
    }

    [Fact]
    public void VectorTable_CanBePlacedAtTheTopOfMemory()
    {
        AssembledProgram program = Assembler.Assemble("HLT\n.org 0xFFF0\n.dw 0x0000, 0x1234");

        byte[] image = program.ToImage();
        Assert.Equal(new byte[] { 0x00, 0x00, 0x34, 0x12 }, image[0xFFF0..0xFFF4]);
        Assert.Equal(Memory.Size, image.Length);
    }

    [Fact]
    public void EntryPoint_IsTheFirstInstructionEvenWhenDataComesFirst()
    {
        AssembledProgram program = Assembler.Assemble("data: .db 1, 2\nstart: MOV A, 1\nHLT");

        Assert.Equal(2, program.EntryPoint);
    }

    [Fact]
    public void Listing_KeepsEveryLineWithItsAddress()
    {
        AssembledProgram program = Assembler.Assemble("; comment\n\nMOV A, 1\nloop:\nJMP loop");

        Assert.Equal(5, program.Listing.Count);
        Assert.Empty(program.Listing[0].Bytes);
        Assert.Equal(3, program.Listing[2].LineNumber);
        Assert.Equal(0, program.Listing[2].Address);
        Assert.Equal(2, program.Listing[4].Address);
        Assert.Equal(new byte[] { 0x70, 2, 0 }, program.Listing[4].Bytes);
        Assert.Same(program.Listing[4], program.FindByAddress(2));
        Assert.False(program.Listing[4].IsData);
    }

    [Theory]
    [InlineData("FOO A, 1", "Unknown mnemonic 'FOO'")]
    [InlineData("MOV 1, A", "Valid forms")]
    [InlineData("ADD A, [R1]", "Valid forms")]
    [InlineData("REP FOO", "Valid forms")]
    [InlineData("JMP nowhere", "Unknown symbol 'nowhere'")]
    [InlineData("MOV A, 256", "does not fit in one byte")]
    [InlineData("MOV A, -129", "does not fit in one byte")]
    [InlineData("MOV SI, 70000", "does not fit in 16 bits")]
    [InlineData("MOV A, [BP+200]", "Displacement 200 does not fit")]
    [InlineData("x: NOP\nx: NOP", "defined more than once")]
    [InlineData("A: NOP", "register name")]
    [InlineData("SI: NOP", "register name")]
    [InlineData(".ds 70000", "does not fit")]
    [InlineData(".org 10\nNOP\n.org 10\nNOP", "emitted twice")]
    [InlineData(".org 70000", "outside memory")]
    [InlineData(".bogus 1", "Unknown directive")]
    [InlineData("MOV A, [1", "Malformed memory operand")]
    [InlineData("MOV A, 1+", "ends with an operator")]
    [InlineData("MOV A, 'ab'", "Malformed character literal")]
    public void Errors_AreReportedWithTheLine(string source, string expectedText)
    {
        AssemblyException ex = Assert.Throws<AssemblyException>(() => Assembler.Assemble(source));

        Assert.Contains(expectedText, ex.Detail);
        Assert.True(ex.Line >= 1);
        Assert.StartsWith($"Line {ex.Line}:", ex.Message);
    }

    [Fact]
    public void Error_ReportsTheRightLineNumber()
    {
        AssemblyException ex = Assert.Throws<AssemblyException>(() => Assembler.Assemble("NOP\nNOP\nBAD\nNOP"));

        Assert.Equal(3, ex.Line);
    }

    [Fact]
    public void Program_ThatOverflowsMemory_IsRejected()
    {
        string source = ".org 0xFFF0\n" + string.Join('\n', Enumerable.Repeat("MOV A, [0x80+R1]", 5));

        AssemblyException ex = Assert.Throws<AssemblyException>(() => Assembler.Assemble(source));

        Assert.Contains("does not fit", ex.Detail);
    }
}
