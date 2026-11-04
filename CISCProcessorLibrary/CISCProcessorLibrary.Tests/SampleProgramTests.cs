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
    [InlineData("strings", "String ops OK")]
    [InlineData("scan", "9")]
    [InlineData("frames", "42")]
    [InlineData("jumptable", "O")]
    [InlineData("interrupt", "1 2 3 4 5")]
    [InlineData("echo", "HELLO, CISC-16!")]
    public void Sample_ProducesTheExpectedOutput(string id, string expected)
    {
        SampleProgram sample = SamplePrograms.Find(id)!;

        Assert.Equal(expected, Output(sample.Source, sample.Input));
    }

    [Theory]
    [InlineData("countdown")]
    [InlineData("sum")]
    [InlineData("fibonacci")]
    [InlineData("factorial")]
    [InlineData("hello")]
    [InlineData("stack")]
    [InlineData("sort")]
    [InlineData("strings")]
    [InlineData("scan")]
    [InlineData("frames")]
    [InlineData("jumptable")]
    [InlineData("interrupt")]
    [InlineData("echo")]
    public void Sample_HaltsNormally_WithACleanStack(string id)
    {
        SampleProgram sample = SamplePrograms.Find(id)!;
        var cpu = new CISCProcessor();
        cpu.LoadProgram(sample.Source);
        if (sample.Input is not null) cpu.InputDevice.Enqueue(sample.Input);

        RunResult result = cpu.Run(20_000);

        Assert.Equal(StopReason.Halted, result.Reason);
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
        Assert.Equal(14, SamplePrograms.All.Count);
    }
}
