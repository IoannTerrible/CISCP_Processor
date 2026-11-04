using CISCProcessorLibrary;
using CISCProcessorLibrary.Timing;
using Xunit;
using static CISCProcessorLibrary.Tests.TestHelpers;

namespace CISCProcessorLibrary.Tests;

public class TimingTests
{
    [Theory]
    [InlineData("NOP", 4)]
    [InlineData("MOV A, 5", 7)]
    [InlineData("MOV A, R1", 7)]
    [InlineData("MOV A, [0x1234]", 13)]
    [InlineData("JMP 0x0003", 10)]
    [InlineData("CALL 0x0003", 17)]
    [InlineData("INC A", 5)]
    public void Classic1975_CostsTheTStatesOfAn8080(string instruction, int cycles)
    {
        var cpu = new CISCProcessor(Microarchitecture.Classic1975);
        cpu.LoadProgram(instruction + "\nHLT");

        Assert.Equal(cycles, cpu.Step().Cycles);
    }

    [Fact]
    public void Classic1975_RetCostsTenLikeThe8080()
    {
        var cpu = new CISCProcessor(Microarchitecture.Classic1975);
        cpu.LoadProgram("CALL 3\nsub: RET");
        cpu.Step();

        Assert.Equal(10, cpu.Step().Cycles);
    }

    [Fact]
    public void Prefetch1985_StraightLineCode_IsFedFromTheQueue()
    {
        var cpu = new CISCProcessor(Microarchitecture.Prefetch1985);
        cpu.LoadProgram("MUL R1\nNOP\nNOP\nNOP\nHLT");

        cpu.Step();   // MUL: the long ALU operation lets the bus interface unit fill the queue
        StepTrace nop = cpu.Step();

        Assert.Contains(nop.MicroOps, op => op.Kind == MicroOpKind.Timing && op.Name == "queue hit");
        Assert.Equal(1, nop.Cycles);
        Assert.True(cpu.Timing.Statistics["words prefetched"] >= 2);
    }

    [Fact]
    public void Prefetch1985_TakenJump_FlushesTheQueue()
    {
        var cpu = new CISCProcessor(Microarchitecture.Prefetch1985);
        cpu.LoadProgram("JMP 3\nHLT\nNOP");

        StepTrace jump = cpu.Step();
        TimingSnapshot afterJump = cpu.Timing.Snapshot();
        StepTrace next = cpu.Step();

        Assert.Contains(jump.MicroOps, op => op.Kind == MicroOpKind.Timing && op.Name == "queue flush");
        Assert.Equal(1, cpu.Timing.Statistics["queue flushes"]);
        Assert.Equal(3, afterJump.PrefetchQueueAddress);          // the queue restarts at the jump target
        Assert.Equal(3, next.Address);
        Assert.Contains(next.MicroOps, op => op.Kind == MicroOpKind.Timing && op.Name is "queue hit" or "queue miss");
    }

    [Fact]
    public void Pipelined1995_ColdCacheMisses_ThenHits()
    {
        var cpu = new CISCProcessor(Microarchitecture.Pipelined1995);
        cpu.LoadProgram("MOV A, [0x2000]\nMOV A, [0x2001]\nHLT");

        StepTrace first = cpu.Step();
        StepTrace second = cpu.Step();

        Assert.Contains(first.MicroOps, op => op.Name == "I-cache miss");
        Assert.Contains(first.MicroOps, op => op.Name == "D-cache miss");
        Assert.Equal(1 + 8 + 8, first.Cycles);
        Assert.Contains(second.MicroOps, op => op.Name == "D-cache hit");
        Assert.Equal(1, second.Cycles);
        TimingSnapshot snapshot = cpu.Timing.Snapshot();
        Assert.Equal(0x2000 / 256, snapshot.DataCacheTags[0]);
        Assert.Equal(16, snapshot.CacheLineSize);
    }

    [Fact]
    public void Pipelined1995_BranchPredictor_LearnsALoop()
    {
        var cpu = new CISCProcessor(Microarchitecture.Pipelined1995);
        cpu.LoadProgram("MOV R0, 10\nagain: LOOP R0, again\nHLT");

        cpu.Run();

        long predicted = cpu.Timing.Statistics["branches predicted"];
        long mispredicted = cpu.Timing.Statistics["branches mispredicted"];
        Assert.True(predicted > mispredicted, $"predicted {predicted}, mispredicted {mispredicted}");
        Assert.True(cpu.Timing.Snapshot().BranchPredictorCounters.Any(c => c >= 2));
    }

    [Theory]
    [InlineData("countdown", 164, 146, 71)]
    [InlineData("sort", 2931, 2276, 397)]
    [InlineData("hello", 569, 490, 183)]
    [InlineData("fibonacci", 790, 636, 217)]
    public void Samples_HaveStableCycleCounts_PerEra(string id, long classic, long prefetch, long pipelined)
    {
        SampleProgram sample = SamplePrograms.Find(id)!;

        Assert.Equal(classic, Run(sample.Source, Microarchitecture.Classic1975).Cycles);
        Assert.Equal(prefetch, Run(sample.Source, Microarchitecture.Prefetch1985).Cycles);
        Assert.Equal(pipelined, Run(sample.Source, Microarchitecture.Pipelined1995).Cycles);
    }

    [Fact]
    public void ResultsDoNotDependOnTheMicroarchitecture()
    {
        foreach (SampleProgram sample in SamplePrograms.All)
        {
            string classic = Output(sample.Source, sample.Input, Microarchitecture.Classic1975);
            Assert.Equal(classic, Output(sample.Source, sample.Input, Microarchitecture.Prefetch1985));
            Assert.Equal(classic, Output(sample.Source, sample.Input, Microarchitecture.Pipelined1995));
        }
    }

    [Fact]
    public void SwitchingTheMicroarchitecture_ResetsTheModel()
    {
        var cpu = new CISCProcessor(Microarchitecture.Classic1975);
        cpu.LoadProgram("NOP\nHLT");
        cpu.Step();

        cpu.Microarchitecture = Microarchitecture.Pipelined1995;

        Assert.Equal(Microarchitecture.Pipelined1995, cpu.Timing.Kind);
        Assert.Empty(cpu.Timing.Statistics);
        Assert.Equal("1995 pipeline + caches (Pentium style)", cpu.Timing.Name);
    }
}
