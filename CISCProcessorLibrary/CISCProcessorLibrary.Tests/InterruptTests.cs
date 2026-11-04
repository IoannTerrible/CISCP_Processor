using CISCProcessorLibrary;
using Xunit;
using static CISCProcessorLibrary.Tests.TestHelpers;

namespace CISCProcessorLibrary.Tests;

public class InterruptTests
{
    [Fact]
    public void SoftwareInterrupt_SavesFlagsAndIp_AndIretRestoresThem()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("""
                MOV A, 0xFF
                ADD A, 1            ; Z and C set
                EI
                INT 2
                MOV R0, A           ; continues here after IRET
                DI
                HLT
            handler:
                MOV A, 9
                CLC                 ; must not leak out: IRET restores the flags
                IRET
                .org 0xFFF4         ; vector 2
                .dw handler
            """);

        cpu.Run();

        Assert.True(cpu.Halted);
        Assert.Equal(9, cpu.ReadRegister(0));
        Assert.True(cpu.IsFlagSet(Flag.Zero));
        Assert.True(cpu.IsFlagSet(Flag.Carry));
        Assert.False(cpu.IsFlagSet(Flag.Interrupt));
        Assert.Equal(0xFFF0, cpu.StackPointer);
        Assert.Equal(FaultKind.None, cpu.Fault);
    }

    [Fact]
    public void InterruptEntry_ClearsI_AndIsItsOwnStep()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("EI\nNOP\nHLT\nhandler: IRET\n.org 0xFFF2\n.dw handler");
        cpu.Step();                       // EI
        cpu.RequestInterrupt(1);

        StepTrace step = cpu.Step();

        Assert.Equal(StepKind.Interrupt, step.Kind);
        Assert.Equal("IRQ1 -> 0x0003", step.Disassembly);
        Assert.Empty(step.Bytes);
        Assert.False(cpu.IsFlagSet(Flag.Interrupt));
        Assert.Equal(3, cpu.InstructionPointer);
        Assert.Equal(0, cpu.PendingInterrupts);
        Assert.Equal(1, cpu.InstructionsExecuted);
        Assert.Contains(step.MicroOps, op => op.Kind == MicroOpKind.Interrupt && op.Value == 1 && op.Address == 3);
        Assert.Equal(1, cpu.Memory.ReadWord(0xFFED));   // return address: the NOP at 0x0001
        Assert.Equal(1 << (int)Flag.Interrupt, cpu.Memory[0xFFEF]);
    }

    [Fact]
    public void Interrupts_AreIgnoredWhileIIsClear()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("NOP\nNOP\nHLT\n.org 0xFFF0\n.dw 0x0100");
        cpu.RequestInterrupt(0);

        cpu.Run();

        Assert.Equal(3, cpu.InstructionsExecuted);
        Assert.Equal(1, cpu.PendingInterrupts);
        Assert.True(cpu.Halted);
    }

    [Fact]
    public void Halt_WithInterruptsEnabled_WaitsInsteadOfStopping()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("EI\nHLT\nMOV A, 1\nDI\nHLT\nhandler: IRET\n.org 0xFFF0\n.dw handler");

        RunResult limited = cpu.Run(20);

        Assert.Equal(StopReason.StepLimit, limited.Reason);
        Assert.True(cpu.Waiting);
        Assert.Equal(CyclePhase.Waiting, cpu.Phase);
        Assert.Equal(StepKind.Wait, cpu.LastStep!.Kind);
        Assert.Equal(2, cpu.InstructionsExecuted);

        cpu.RequestInterrupt(0);
        RunResult result = cpu.Run();

        Assert.Equal(StopReason.Halted, result.Reason);
        Assert.Equal(1, cpu.Accumulator);
        Assert.False(cpu.Waiting);
    }

    [Fact]
    public void Timer_RaisesIrq0_EveryPeriod()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("MOV A, 100\nOUT 2, A\nMOV A, 1\nOUT 4, A\nEI\nloop: JMP loop\nhandler: INC R0\nIRET\n.org 0xFFF0\n.dw handler");

        cpu.Run(400);

        Assert.True(cpu.Timer.Enabled);
        Assert.Equal(100, cpu.Timer.Period);
        Assert.True(cpu.Timer.Ticks >= 5);
        Assert.Equal(cpu.Timer.Ticks, cpu.ReadRegister(0));
        Assert.Equal(cpu.Timer.Ticks, cpu.Timing.Statistics["interrupts"]);
    }

    [Fact]
    public void TimerSample_PrintsFiveTicks_OnEveryMicroarchitecture()
    {
        foreach (Timing.Microarchitecture arch in Enum.GetValues<Timing.Microarchitecture>())
        {
            Assert.Equal("1 2 3 4 5", Output(SamplePrograms.Interrupt.Source, null, arch));
        }
    }

    [Fact]
    public void LowerIrqNumbers_HaveHigherPriority()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("EI\nNOP\nHLT\nh0: IRET\nh5: IRET\n.org 0xFFF0\n.dw h0, 0, 0, 0, 0, h5");
        cpu.Step();
        cpu.RequestInterrupt(5);
        cpu.RequestInterrupt(0);

        StepTrace first = cpu.Step();

        Assert.Equal("IRQ0 -> 0x0003", first.Disassembly);
        Assert.Equal(1 << 5, cpu.PendingInterrupts);
    }
}
