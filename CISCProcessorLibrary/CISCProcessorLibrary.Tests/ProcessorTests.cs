using CISCProcessorLibrary;
using Xunit;
using static CISCProcessorLibrary.Tests.TestHelpers;

namespace CISCProcessorLibrary.Tests;

public class ProcessorTests
{
    [Fact]
    public void Reset_State_IsPowerOn()
    {
        var cpu = new CISCProcessor();

        Assert.Equal(0, cpu.Accumulator);
        Assert.Equal(0, cpu.InstructionPointer);
        Assert.Equal(CISCProcessor.InitialStackPointer, cpu.StackPointer);
        Assert.Equal(0, cpu.FlagRegister);
        Assert.Equal(CyclePhase.Idle, cpu.Phase);
        Assert.False(cpu.Halted);
        Assert.Equal(FaultKind.None, cpu.Fault);
        Assert.All(cpu.Registers, r => Assert.Equal(0, r));
    }

    [Fact]
    public void Step_FetchesDecodesAndExecutesOneInstruction()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("MOV A, 7\nHLT");

        StepTrace step = cpu.Step();

        Assert.Equal(0, step.Address);
        Assert.Equal(new byte[] { 0x10, 7 }, step.Bytes);
        Assert.Equal("MOV A, 7", step.Disassembly);
        Assert.Equal(2, step.Cycles);
        Assert.Equal(7, cpu.Accumulator);
        Assert.Equal(2, cpu.InstructionPointer);
        Assert.Equal(0x10, cpu.InstructionRegister);
        Assert.Equal(CyclePhase.Execute, cpu.Phase);
        Assert.Equal(1, cpu.InstructionsExecuted);
        Assert.Equal(MicroOpKind.Fetch, step.MicroOps[0].Kind);
        Assert.Contains(step.MicroOps, op => op.Kind == MicroOpKind.Decode && op.Text == "MOV A, 7");
        Assert.Contains(step.MicroOps, op => op.Kind == MicroOpKind.Register && op.Name == "A" && op.After == 7);
        Assert.Equal(7, step.StateAfter.Accumulator);
    }

    [Fact]
    public void Halt_StopsTheProcessor_AndSteppingAgainThrows()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("HLT\nMOV A, 1");

        RunResult result = cpu.Run();

        Assert.Equal(StopReason.Halted, result.Reason);
        Assert.True(cpu.Halted);
        Assert.Equal(CyclePhase.Halted, cpu.Phase);
        Assert.Equal(0, cpu.Accumulator);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
    }

    [Fact]
    public void Run_StopsAtTheInstructionLimit_ForEndlessLoops()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("loop: JMP loop");

        RunResult result = cpu.Run(50);

        Assert.Equal(StopReason.InstructionLimit, result.Reason);
        Assert.Equal(50, result.InstructionsExecuted);
        Assert.False(cpu.Halted);
    }

    [Theory]
    [InlineData("MOV A, 0\nCMP A, 0\nJZ ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 2)]
    [InlineData("MOV A, 1\nCMP A, 0\nJZ ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 1)]
    [InlineData("MOV A, 1\nCMP A, 0\nJNZ ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 2)]
    [InlineData("MOV A, 1\nSUB A, 2\nJC ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 2)]
    [InlineData("MOV A, 3\nSUB A, 2\nJNC ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 2)]
    [InlineData("MOV A, 0x80\nADD A, 0\nJS ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 2)]
    [InlineData("MOV A, 0x10\nADD A, 0\nJNS ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 2)]
    [InlineData("MOV A, 0x7F\nADD A, 1\nJO ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 2)]
    [InlineData("MOV A, 0x10\nADD A, 1\nJNO ok\nMOV R0, 1\nHLT\nok: MOV R0, 2\nHLT", 2)]
    public void ConditionalJumps_FollowTheFlags(string source, int expectedR0)
    {
        Assert.Equal(expectedR0, Run(source).ReadRegister(0));
    }

    [Fact]
    public void TakenJump_CostsOneExtraCycle()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("JMP 2\nHLT");

        StepTrace step = cpu.Step();

        Assert.Equal(3, step.Cycles);
        Assert.Contains(step.MicroOps, op => op.Kind == MicroOpKind.Jump && op.Value == 1 && op.Address == 2);
    }

    [Fact]
    public void Call_PushesTheReturnAddress_AndRet_PopsIt()
    {
        var cpu = new CISCProcessor();
        cpu.LoadAndRemember("""
                CALL sub
                MOV R0, 1
                HLT
            sub: MOV R1, 2
                RET
            """);

        cpu.Step();
        Assert.Equal(0xFE, cpu.StackPointer);
        Assert.Equal(2, cpu.Memory[0xFF]);
        Assert.Equal(cpu.LoadedSymbol("sub"), cpu.InstructionPointer);

        cpu.Run();
        Assert.Equal(1, cpu.ReadRegister(0));
        Assert.Equal(2, cpu.ReadRegister(1));
        Assert.Equal(0xFF, cpu.StackPointer);
    }

    [Fact]
    public void PushAndPop_RoundTripRegistersAndFlags()
    {
        CISCProcessor cpu = Run("""
                MOV A, 0xFF
                ADD A, 1            ; Z and C set
                PUSH FLAGS
                MOV R1, 0x11
                PUSH R1
                PUSH A
                CLC
                MOV R1, 0
                POP R2              ; R2 = 0 (old A)
                POP R3              ; R3 = 0x11
                POP FLAGS
                HLT
            """);

        Assert.Equal(0, cpu.ReadRegister(2));
        Assert.Equal(0x11, cpu.ReadRegister(3));
        Assert.True(cpu.IsFlagSet(Flag.Zero));
        Assert.True(cpu.IsFlagSet(Flag.Carry));
        Assert.Equal(0xFF, cpu.StackPointer);
    }

    [Fact]
    public void Pop_OnEmptyStack_FaultsWithUnderflow()
    {
        CISCProcessor cpu = Run("POP A\nHLT");

        Assert.Equal(FaultKind.StackUnderflow, cpu.Fault);
        Assert.True(cpu.Halted);
        Assert.Contains(cpu.LastStep!.MicroOps, op => op.Kind == MicroOpKind.Fault);
    }

    [Fact]
    public void Ret_OnEmptyStack_FaultsWithUnderflow()
    {
        Assert.Equal(FaultKind.StackUnderflow, Run("RET").Fault);
    }

    [Fact]
    public void Push_WhenSpIsZero_FaultsWithOverflow()
    {
        CISCProcessor cpu = Run("MOV SP, 0\nPUSH A\nHLT");

        Assert.Equal(FaultKind.StackOverflow, cpu.Fault);
        Assert.Equal(StopReason.Fault, cpu.Run(1).Reason);
    }

    [Fact]
    public void InvalidOpcode_Faults()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram(new byte[] { 0xFF });

        RunResult result = cpu.Run();

        Assert.Equal(StopReason.Fault, result.Reason);
        Assert.Equal(FaultKind.InvalidOpcode, result.Fault);
        Assert.Contains("0xFF", result.FaultMessage);
        Assert.Equal(0, result.InstructionsExecuted);
    }

    [Fact]
    public void FaultingInstruction_LeavesTheInstructionCountUnchanged_ButIsTraced()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("MOV R0, 0\nMOV A, 5\nDIV R0");

        StepTrace? faulted = null;
        cpu.InstructionExecuted += (_, step) => faulted = step;
        cpu.Run();

        Assert.NotNull(faulted);
        Assert.Equal("DIV R0", faulted!.Disassembly);
        Assert.Equal(2, cpu.InstructionsExecuted);
        Assert.Equal(FaultKind.DivideByZero, faulted.StateAfter.Fault);
    }

    [Fact]
    public void IndexedAddressing_WrapsAroundTheAddressSpace()
    {
        CISCProcessor cpu = Run("MOV R1, 2\nMOV A, [0xFF+R1]\nHLT");

        // 0xFF + 2 wraps to 0x01, which holds the operand byte of "MOV R1, 2" (register index 1).
        Assert.Equal(1, cpu.Accumulator);
    }

    [Fact]
    public void RegisterIndirect_ReadsAndWritesThroughRn()
    {
        CISCProcessor cpu = Run("MOV R0, 0x80\nMOV A, 9\nMOV [R0], A\nMOV R1, 0x80\nMOV A, 0\nMOV A, [R1]\nHLT");

        Assert.Equal(9, cpu.Memory[0x80]);
        Assert.Equal(9, cpu.Accumulator);
    }

    [Fact]
    public void Exchange_SwapsAccumulatorAndRegister()
    {
        CISCProcessor cpu = Run("MOV A, 1\nMOV R4, 2\nXCHG A, R4\nHLT");

        Assert.Equal(2, cpu.Accumulator);
        Assert.Equal(1, cpu.ReadRegister(4));
    }

    [Fact]
    public void Loop_DecrementsAndBranches_WithoutTouchingFlags()
    {
        CISCProcessor cpu = Run("STC\nMOV R0, 3\nMOV R1, 0\nagain: INC R1\nLOOP R0, again\nHLT");

        Assert.Equal(3, cpu.ReadRegister(1));
        Assert.Equal(0, cpu.ReadRegister(0));
    }

    [Fact]
    public void Cycles_AreAccumulated()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram(SamplePrograms.Countdown.Source);

        RunResult result = cpu.Run();

        Assert.Equal(17, result.InstructionsExecuted);
        Assert.Equal(42, result.Cycles);
    }

    [Fact]
    public void Out_RaisesOutputWritten()
    {
        var cpu = new CISCProcessor();
        var received = new List<(byte Port, byte Value)>();
        cpu.OutputWritten += (_, e) => received.Add((e.Port, e.Value));
        cpu.LoadProgram("MOV A, 'H'\nOUT 1, A\nMOV A, 42\nOUT 0, A\nHLT");

        cpu.Run();

        Assert.Equal(new[] { ((byte)1, (byte)'H'), ((byte)0, (byte)42) }, received);
    }

    [Fact]
    public void MicroOperation_EventFiresForEveryMicroOp()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("NOP");
        int count = 0;
        cpu.MicroOperation += (_, _) => count++;

        StepTrace step = cpu.Step();

        Assert.Equal(step.MicroOps.Count, count);
    }

    [Fact]
    public void WriteRegister_UsesOnlyTheLowThreeBitsOfTheIndex()
    {
        var cpu = new CISCProcessor();

        cpu.WriteRegister(9, 5);

        Assert.Equal(5, cpu.ReadRegister(1));
    }

    [Fact]
    public void GetState_IsASnapshot()
    {
        var cpu = new CISCProcessor();
        cpu.WriteRegister(0, 1);
        ProcessorState state = cpu.GetState();

        cpu.WriteRegister(0, 2);

        Assert.Equal(1, state.Registers[0]);
        Assert.Equal(2, cpu.ReadRegister(0));
    }

    [Fact]
    public void LoadProgram_ResetsRegistersButKeepsMemoryOnPlainReset()
    {
        var cpu = new CISCProcessor();
        cpu.LoadProgram("MOV A, 1\nMOV [0x80], A\nHLT");
        cpu.Run();

        cpu.Reset();

        Assert.Equal(0, cpu.Accumulator);
        Assert.False(cpu.Halted);
        Assert.Equal(1, cpu.Memory[0x80]);

        cpu.Reset(clearMemory: true);
        Assert.Equal(0, cpu.Memory[0x80]);
    }

    [Fact]
    public void FlagRegister_OnlyKeepsTheFourDefinedBits()
    {
        var cpu = new CISCProcessor();

        cpu.FlagRegister = 0xFF;

        Assert.Equal(0x0F, cpu.FlagRegister);
        Assert.Equal("ZCNV", cpu.GetState().FlagsText);
    }
}
