using CISCProcessorLibrary;
using Xunit;
using static CISCProcessorLibrary.Tests.TestHelpers;

namespace CISCProcessorLibrary.Tests;

public class StringTests
{
    [Fact]
    public void Lodsb_LoadsAndAdvancesSi()
    {
        CISCProcessor cpu = Run("MOV SI, text\nLODSB\nMOV R0, A\nLODSB\nHLT\ntext: .db 'a', 'b'");

        Assert.Equal((byte)'a', cpu.ReadRegister(0));
        Assert.Equal((byte)'b', cpu.Accumulator);
        Assert.Equal(cpu.LoadedSymbol("text") + 2, cpu.SourceIndex);
    }

    [Fact]
    public void RepMovsb_CopiesR6Bytes()
    {
        CISCProcessor cpu = Run("MOV SI, src\nMOV DI, dst\nMOV R6, 4\nREP MOVSB\nHLT\nsrc: .db 1, 2, 3, 4, 5\ndst: .ds 5");

        int dst = cpu.LoadedSymbol("dst");
        Assert.Equal(new byte[] { 1, 2, 3, 4, 0 }, cpu.Memory.ToArray()[dst..(dst + 5)]);
        Assert.Equal(0, cpu.ReadRegister(6));
        Assert.Equal(dst + 4, cpu.DestinationIndex);
    }

    [Fact]
    public void RepStosb_FillsMemory()
    {
        CISCProcessor cpu = Run("MOV DI, 0x2000\nMOV A, 0xAA\nMOV R6, 3\nREP STOSB\nHLT");

        Assert.Equal(0xAA, cpu.Memory[0x2000]);
        Assert.Equal(0xAA, cpu.Memory[0x2002]);
        Assert.Equal(0, cpu.Memory[0x2003]);
        Assert.Equal(0x2003, cpu.DestinationIndex);
    }

    [Fact]
    public void RepCmpsb_StopsAtTheFirstDifference()
    {
        CISCProcessor cpu = Run("MOV SI, s1\nMOV DI, s2\nMOV R6, 5\nREP CMPSB\nHLT\ns1: .db \"abcde\"\ns2: .db \"abXde\"");

        Assert.False(cpu.IsFlagSet(Flag.Zero));
        Assert.Equal(2, cpu.ReadRegister(6));                 // two comparisons were left
        Assert.Equal(cpu.LoadedSymbol("s1") + 3, cpu.SourceIndex);
    }

    [Fact]
    public void RepScasb_FindsAByte()
    {
        Assert.Equal("9", Output(SamplePrograms.Scan.Source));
    }

    [Fact]
    public void RepWithZeroCount_DoesNothing()
    {
        CISCProcessor cpu = Run("MOV SI, 0x10\nMOV DI, 0x20\nMOV R6, 0\nREP MOVSB\nHLT");

        Assert.Equal(0x10, cpu.SourceIndex);
        Assert.Equal(0x20, cpu.DestinationIndex);
    }
}
