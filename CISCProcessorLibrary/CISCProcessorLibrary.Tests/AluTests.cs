using CISCProcessorLibrary;
using Xunit;
using static CISCProcessorLibrary.Tests.TestHelpers;

namespace CISCProcessorLibrary.Tests;

public class AluTests
{
    [Fact]
    public void Add_WrapsAndSetsZeroAndCarry()
    {
        CISCProcessor cpu = Run("MOV A, 0xFF\nADD A, 1\nHLT");

        Assert.Equal(0x00, cpu.Accumulator);
        Assert.True(cpu.IsFlagSet(Flag.Zero));
        Assert.True(cpu.IsFlagSet(Flag.Carry));
        Assert.False(cpu.IsFlagSet(Flag.Negative));
        Assert.False(cpu.IsFlagSet(Flag.Overflow));
    }

    [Fact]
    public void Add_DetectsSignedOverflow()
    {
        CISCProcessor cpu = Run("MOV A, 0x7F\nADD A, 1\nHLT");

        Assert.Equal(0x80, cpu.Accumulator);
        Assert.True(cpu.IsFlagSet(Flag.Overflow));
        Assert.True(cpu.IsFlagSet(Flag.Negative));
        Assert.False(cpu.IsFlagSet(Flag.Carry));
    }

    [Fact]
    public void Adc_AddsTheCarryFlag()
    {
        CISCProcessor cpu = Run("STC\nMOV A, 5\nADC A, 5\nHLT");

        Assert.Equal(11, cpu.Accumulator);
        Assert.False(cpu.IsFlagSet(Flag.Carry));
    }

    [Fact]
    public void Sub_SetsBorrowAndNegative()
    {
        CISCProcessor cpu = Run("MOV A, 5\nSUB A, 10\nHLT");

        Assert.Equal(0xFB, cpu.Accumulator);
        Assert.True(cpu.IsFlagSet(Flag.Carry));
        Assert.True(cpu.IsFlagSet(Flag.Negative));
        Assert.False(cpu.IsFlagSet(Flag.Overflow));
    }

    [Fact]
    public void Sbb_SubtractsTheBorrow()
    {
        CISCProcessor cpu = Run("STC\nMOV A, 10\nMOV R1, 3\nSBB A, R1\nHLT");

        Assert.Equal(6, cpu.Accumulator);
    }

    [Fact]
    public void Cmp_OnlyChangesFlags()
    {
        CISCProcessor cpu = Run("MOV A, 7\nCMP A, 7\nHLT");

        Assert.Equal(7, cpu.Accumulator);
        Assert.True(cpu.IsFlagSet(Flag.Zero));
        Assert.False(cpu.IsFlagSet(Flag.Carry));
    }

    [Theory]
    [InlineData("AND A, 0x0F", 0xF0, 0x00, true)]
    [InlineData("OR A, 0x0F", 0xF0, 0xFF, false)]
    [InlineData("XOR A, 0xFF", 0xF0, 0x0F, false)]
    [InlineData("NOT A", 0xF0, 0x0F, false)]
    public void Logic_ClearsCarryAndOverflow(string instruction, int input, int expected, bool zero)
    {
        CISCProcessor cpu = Run($"STC\nMOV A, {input}\n{instruction}\nHLT");

        Assert.Equal(expected, cpu.Accumulator);
        Assert.Equal(zero, cpu.IsFlagSet(Flag.Zero));
        Assert.False(cpu.IsFlagSet(Flag.Carry));
        Assert.False(cpu.IsFlagSet(Flag.Overflow));
    }

    [Theory]
    [InlineData("SHL A", 0x81, 0x02, true)]
    [InlineData("SHR A", 0x81, 0x40, true)]
    [InlineData("ROL A", 0x81, 0x03, true)]
    [InlineData("ROR A", 0x01, 0x80, true)]
    [InlineData("SHL A", 0x40, 0x80, false)]
    public void Shifts_MoveTheOutgoingBitIntoCarry(string instruction, int input, int expected, bool carry)
    {
        CISCProcessor cpu = Run($"MOV A, {input}\n{instruction}\nHLT");

        Assert.Equal(expected, cpu.Accumulator);
        Assert.Equal(carry, cpu.IsFlagSet(Flag.Carry));
    }

    [Fact]
    public void Inc_LeavesCarryAlone()
    {
        CISCProcessor cpu = Run("STC\nMOV A, 0xFF\nINC A\nHLT");

        Assert.Equal(0, cpu.Accumulator);
        Assert.True(cpu.IsFlagSet(Flag.Zero));
        Assert.True(cpu.IsFlagSet(Flag.Carry));
    }

    [Fact]
    public void Dec_SetsOverflowAtTheSignedBoundary()
    {
        CISCProcessor cpu = Run("MOV R2, 0x80\nDEC R2\nHLT");

        Assert.Equal(0x7F, cpu.ReadRegister(2));
        Assert.True(cpu.IsFlagSet(Flag.Overflow));
        Assert.False(cpu.IsFlagSet(Flag.Negative));
    }

    [Fact]
    public void Inc_OnMemoryIsReadModifyWrite()
    {
        CISCProcessor cpu = Run("INC [counter]\nINC [counter]\nHLT\ncounter: .db 40");

        Assert.Equal(42, cpu.Memory[cpu.LoadedSymbol("counter")]);
    }

    [Theory]
    [InlineData(1, 0xFF, true, false)]
    [InlineData(0, 0x00, false, true)]
    [InlineData(0x80, 0x80, true, false)]
    public void Neg_ComputesTwosComplement(int input, int expected, bool carry, bool zero)
    {
        CISCProcessor cpu = Run($"MOV A, {input}\nNEG A\nHLT");

        Assert.Equal(expected, cpu.Accumulator);
        Assert.Equal(carry, cpu.IsFlagSet(Flag.Carry));
        Assert.Equal(zero, cpu.IsFlagSet(Flag.Zero));
    }

    [Fact]
    public void Mul_KeepsTheLowByteAndFlagsOverflow()
    {
        CISCProcessor cpu = Run("MOV A, 16\nMOV R0, 16\nMUL R0\nHLT");

        Assert.Equal(0, cpu.Accumulator);
        Assert.True(cpu.IsFlagSet(Flag.Carry));
        Assert.True(cpu.IsFlagSet(Flag.Overflow));
        Assert.True(cpu.IsFlagSet(Flag.Zero));
    }

    [Fact]
    public void Div_And_Mod_AreUnsigned()
    {
        Assert.Equal("14 2", Output("MOV A, 100\nMOV R0, 7\nDIV R0\nOUT 0, A\nMOV A, 100\nMOD R0\nOUT 0, A\nHLT"));
    }

    [Fact]
    public void Div_ByZero_Faults()
    {
        CISCProcessor cpu = Run("MOV A, 1\nMOV R0, 0\nDIV R0\nHLT");

        Assert.True(cpu.Halted);
        Assert.Equal(FaultKind.DivideByZero, cpu.Fault);
        Assert.Equal(1, cpu.Accumulator);
    }
}
