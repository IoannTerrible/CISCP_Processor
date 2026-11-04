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
    [InlineData("MOV A, [0x80]", new byte[] { 0x15, 0x80 })]
    [InlineData("MOV [0x80], A", new byte[] { 0x16, 0x80 })]
    [InlineData("MOV A, [R1]", new byte[] { 0x17, 1 })]
    [InlineData("MOV [R7], A", new byte[] { 0x18, 7 })]
    [InlineData("MOV A, [0x80+R1]", new byte[] { 0x19, 0x80, 1 })]
    [InlineData("MOV A, [R1+0x80]", new byte[] { 0x19, 0x80, 1 })]
    [InlineData("MOV [0x40+R2], A", new byte[] { 0x1A, 0x40, 2 })]
    [InlineData("MOV R4, [0x20]", new byte[] { 0x1B, 4, 0x20 })]
    [InlineData("MOV [0x20], R3", new byte[] { 0x1C, 0x20, 3 })]
    [InlineData("MOV SP, 0xC0", new byte[] { 0x1D, 0xC0 })]
    [InlineData("XCHG A, R1", new byte[] { 0x1E, 1 })]
    [InlineData("PUSH FLAGS", new byte[] { 0x22 })]
    [InlineData("POP R2", new byte[] { 0x25, 2 })]
    [InlineData("ADD A, [0x10]", new byte[] { 0x32, 0x10 })]
    [InlineData("CMP A, R0", new byte[] { 0x3D, 0 })]
    [InlineData("INC [0x33]", new byte[] { 0x42, 0x33 })]
    [InlineData("MUL R2", new byte[] { 0x46, 2 })]
    [InlineData("JNZ 0x10", new byte[] { 0x62, 0x10 })]
    [InlineData("CALL 0x20", new byte[] { 0x69, 0x20 })]
    [InlineData("LOOP R0, 0x05", new byte[] { 0x6B, 0, 5 })]
    [InlineData("OUT 1, A", new byte[] { 0xF0, 1 })]
    public void Encodes_EveryAddressingMode(string source, byte[] expected)
    {
        Assert.Equal(expected, Bytes(source));
    }

    [Fact]
    public void Mnemonics_AndRegisters_AreCaseInsensitive()
    {
        Assert.Equal(new byte[] { 0x19, 0x80, 1 }, Bytes("mov a, [0x80+r1]"));
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

        Assert.Equal(new byte[] { 0x60, 0x03, 0x00, 0x01 }, bytes);
    }

    [Fact]
    public void Labels_WorkInExpressionsAndIndexedOperands()
    {
        AssembledProgram program = Assembler.Assemble("""
                MOV A, [table+1]
                MOV A, [table+R2]
                MOV R0, table
                HLT
            table: .db 1, 2, 3
            """);

        int table = program.Symbols["table"];
        Assert.Equal(9, table);
        Assert.Equal(new byte[] { 0x15, (byte)(table + 1) }, program.Listing[0].Bytes);
        Assert.Equal(new byte[] { 0x19, (byte)table, 2 }, program.Listing[1].Bytes);
        Assert.Equal(new byte[] { 0x11, 0, (byte)table }, program.Listing[2].Bytes);
    }

    [Fact]
    public void Directives_OrgDbDsEqu()
    {
        AssembledProgram program = Assembler.Assemble("""
                .equ START, 0x20
                .equ LIMIT, START + 2
                .org START
                MOV A, LIMIT
            buffer: .ds 3
            text:   .db "Hi;", 0, 'x'
                HLT
            """);

        Assert.Equal(0x20, program.EntryPoint);
        Assert.Equal(0x22, program.Symbols["LIMIT"]);
        Assert.Equal(0x22, program.Symbols["buffer"]);
        Assert.Equal(0x25, program.Symbols["text"]);
        byte[] image = program.ToImage();
        Assert.Equal(new byte[] { 0x10, 0x22 }, image[0x20..0x22]);
        Assert.Equal(new byte[] { 0, 0, 0 }, image[0x22..0x25]);
        Assert.Equal(new byte[] { (byte)'H', (byte)'i', (byte)';', 0, (byte)'x', 0x01 }, image[0x25..0x2B]);
        Assert.Equal(2 + 3 + 5 + 1, program.Length);
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
        Assert.Equal(new byte[] { 0x60, 2 }, program.Listing[4].Bytes);
        Assert.Same(program.Listing[4], program.FindByAddress(2));
    }

    [Theory]
    [InlineData("FOO A, 1", "Unknown mnemonic 'FOO'")]
    [InlineData("MOV 1, A", "Valid forms")]
    [InlineData("ADD A, [R1]", "Valid forms")]
    [InlineData("JMP nowhere", "Unknown symbol 'nowhere'")]
    [InlineData("MOV A, 256", "does not fit in one byte")]
    [InlineData("MOV A, -129", "does not fit in one byte")]
    [InlineData("x: NOP\nx: NOP", "defined more than once")]
    [InlineData("A: NOP", "register name")]
    [InlineData(".ds 300", "does not fit")]
    [InlineData(".org 10\nNOP\n.org 10\nNOP", "emitted twice")]
    [InlineData(".org 300", "outside memory")]
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
        string source = string.Join('\n', Enumerable.Repeat("MOV A, [0x80+R1]", 90));

        AssemblyException ex = Assert.Throws<AssemblyException>(() => Assembler.Assemble(source));

        Assert.Contains("does not fit", ex.Detail);
    }
}
