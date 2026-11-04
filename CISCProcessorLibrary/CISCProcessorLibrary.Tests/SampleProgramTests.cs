using CISCProcessorLibrary;
using Xunit;
using static CISCProcessorLibrary.Tests.TestHelpers;

namespace CISCProcessorLibrary.Tests;

public class SampleProgramTests
{
    [Theory]
    [InlineData("countdown", "5 4 3 2 1")]
    [InlineData("sum", "210")]
    [InlineData("fibonacci", "1 1 2 3 5 8 13 21 34 55 89 144 233")]
    [InlineData("factorial", "120")]
    [InlineData("hello", "Hello, CISC!")]
    [InlineData("stack", "14 6")]
    [InlineData("sort", "3 7 19 25 42 88")]
    public void Sample_ProducesTheExpectedOutput(string id, string expected)
    {
        SampleProgram sample = SamplePrograms.Find(id)!;

        Assert.Equal(expected, Output(sample.Source));
    }

    [Theory]
    [InlineData("countdown")]
    [InlineData("sum")]
    [InlineData("fibonacci")]
    [InlineData("factorial")]
    [InlineData("hello")]
    [InlineData("stack")]
    [InlineData("sort")]
    public void Sample_HaltsNormally_WithACleanStack(string id)
    {
        CISCProcessor cpu = Run(SamplePrograms.Find(id)!.Source);

        Assert.True(cpu.Halted);
        Assert.Equal(FaultKind.None, cpu.Fault);
        Assert.Equal(CISCProcessor.InitialStackPointer, cpu.StackPointer);
    }

    [Fact]
    public void FaultSample_StopsWithDivideByZero()
    {
        CISCProcessor cpu = Run(SamplePrograms.DivideByZero.Source);

        Assert.Equal(FaultKind.DivideByZero, cpu.Fault);
    }

    [Fact]
    public void EverySample_HasAUniqueId()
    {
        Assert.Equal(SamplePrograms.All.Count, SamplePrograms.All.Select(s => s.Id).Distinct().Count());
        Assert.Null(SamplePrograms.Find("does-not-exist"));
    }
}
