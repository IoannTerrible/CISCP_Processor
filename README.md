# CISC Processor Simulator 🚀

**Version 0.10** - an 8 bit CISC machine ("CISC-8") written in C#, with a real fetch - decode - execute cycle, a two pass assembler, a console debugger and an animated web visualizer that shows every bus transfer, ALU operation and register write while a program runs.

![CISC-8 processor visualizer](docs/visualizer.png)

## What is in the box ℹ️

| Project | What it is |
|---|---|
| `CISCProcessorLibrary/CISCProcessorLibrary` | The processor core: registers, memory, ALU, hardware stack, instruction set, assembler, disassembler, execution tracing. No dependencies. |
| `CISCProcessorLibrary/Demo` | Console front end: interactive menu, runs `.asm` files or the built-in samples, prints a colored execution trace, micro-operations, registers and a memory dump. |
| `CISCProcessorLibrary/CISCProcessorWeb` | ASP.NET Core host that serves the visualizer and exposes a JSON API so the page runs programs on the .NET core. |
| `CISCProcessorLibrary/CISCProcessorLibrary.Tests` | xunit test suite (ALU flags, assembler encodings and errors, processor behaviour, faults, sample programs, disassembler round trips). |
| `web/` | The visualizer: a static page with a JavaScript mirror of the processor, so it also works offline or from GitHub Pages. |

## The CISC-8 machine 🧠

- **Registers**: accumulator `A`, eight general purpose registers `R0`..`R7`, instruction pointer `IP`, stack pointer `SP`, status register `FLAGS`, plus the internal `IR` (instruction register), `MAR` (memory address register) and `MDR` (memory data register) that drive the buses.
- **Memory**: 256 bytes of byte addressable RAM shared by code, data and the hardware stack. The stack grows downwards from `0xFF`; `PUSH` stores at `SP` and decrements it, `POP` does the opposite.
- **Flags**: `Z` zero, `C` carry / borrow, `N` negative (bit 7), `V` signed overflow. They are computed by the ALU exactly like on a classic 8 bit CPU: `ADD` sets `C` on an unsigned carry and `V` on a signed overflow, `SUB` and `CMP` set `C` on a borrow, logic operations clear `C` and `V`, shifts move the outgoing bit into `C`, `INC` and `DEC` leave `C` alone.
- **Instruction cycle**: every instruction goes through *fetch* (opcode read into `IR`, `IP` incremented), *decode* (opcode lookup, operand bytes read) and *execute*. Each phase, bus transfer, ALU result, register write, jump decision and port output is reported as a **micro-operation**, which is what the trace views and the animation are built from.
- **Variable length CISC encoding**: one opcode byte followed by zero, one or two operand bytes. Addressing modes: register, immediate, direct `[addr]`, register indirect `[Rn]` and indexed `[addr+Rn]`. Complex instructions such as `LOOP Rn, addr` (decrement, test and branch), `MUL` / `DIV` / `MOD`, `XCHG`, `INC [addr]` (read-modify-write) and `CALL` / `RET` do in one instruction what a RISC machine needs several for.
- **Timing**: every instruction has a cycle cost (1 for a register move, 12 for a division); taken branches cost one extra cycle. The cycle counter is part of the processor state.
- **Faults**: an invalid opcode, a stack overflow or underflow and a division by zero halt the processor with a `FaultKind` and a message instead of throwing out of the simulation.
- **Output**: `OUT port, A` writes the accumulator to port 0 (shown as a number) or port 1 (shown as a character).

### Assembly language

```asm
; Add the bytes stored at 'data' and print the sum.
        MOV R0, 6           ; element count
        MOV R1, 0           ; index
        MOV R2, 0           ; running sum
next:   MOV A, [data+R1]    ; indexed addressing: A = data[R1]
        ADD A, R2
        MOV R2, A
        INC R1
        LOOP R0, next       ; R0 = R0 - 1, jump while R0 != 0
        MOV A, R2
        OUT 0, A            ; prints 210
        HLT
data:   .db 10, 20, 30, 40, 50, 60
```

- One statement per line: `[label:] MNEMONIC [operand[, operand]] [; comment]`. Mnemonics, registers and labels are case insensitive.
- Operands: `A`, `R0`..`R7`, `SP`, `FLAGS`, a number or label (immediate or address), `[addr]`, `[Rn]`, `[addr+Rn]`.
- Numbers: decimal `42`, hex `0x2A` or `$2A`, binary `0b101010`, characters `'A'`, negative values `-1` (stored as two's complement). Labels and numbers can be combined with `+` and `-`, for example `[table+2]`.
- Directives: `.org addr` sets the location counter, `.db v, v, "text"` emits bytes, `.ds count` reserves zeroed bytes, `.equ NAME, value` defines a constant.
- The assembler reports errors with the line number and, for a wrong operand combination, lists the valid forms of the mnemonic.

### Instruction set

74 instructions. `imm8` is an 8 bit constant, `addr` an 8 bit address, `Rn` / `Rm` a register `R0`..`R7`.

**Control**

| Opcode | Syntax | Bytes | Cycles | Description |
|---|---|---|---|---|
| `00` | `NOP` | 1 | 1 | No operation |
| `01` | `HLT` | 1 | 1 | Halt the processor |

**Data movement**

| Opcode | Syntax | Bytes | Cycles | Description |
|---|---|---|---|---|
| `10` | `MOV A, imm8` | 2 | 2 | Load an immediate into A |
| `11` | `MOV Rn, imm8` | 3 | 2 | Load an immediate into Rn |
| `12` | `MOV A, Rn` | 2 | 1 | Copy Rn to A |
| `13` | `MOV Rn, A` | 2 | 1 | Copy A to Rn |
| `14` | `MOV Rn, Rm` | 3 | 1 | Copy Rm to Rn |
| `15` | `MOV A, [addr]` | 2 | 3 | Load A from memory (direct) |
| `16` | `MOV [addr], A` | 2 | 3 | Store A to memory (direct) |
| `17` | `MOV A, [Rn]` | 2 | 3 | Load A from the address held in Rn (register indirect) |
| `18` | `MOV [Rn], A` | 2 | 3 | Store A to the address held in Rn (register indirect) |
| `19` | `MOV A, [addr+Rn]` | 3 | 4 | Load A from addr + Rn (indexed) |
| `1A` | `MOV [addr+Rn], A` | 3 | 4 | Store A to addr + Rn (indexed) |
| `1B` | `MOV Rn, [addr]` | 3 | 3 | Load Rn from memory (direct) |
| `1C` | `MOV [addr], Rn` | 3 | 3 | Store Rn to memory (direct) |
| `1D` | `MOV SP, imm8` | 2 | 2 | Set the stack pointer |
| `1E` | `XCHG A, Rn` | 2 | 2 | Swap A and Rn |

**Stack**

| Opcode | Syntax | Bytes | Cycles | Description |
|---|---|---|---|---|
| `20` | `PUSH A` | 1 | 3 | Push A |
| `21` | `PUSH Rn` | 2 | 3 | Push Rn |
| `22` | `PUSH FLAGS` | 1 | 3 | Push the flags register |
| `24` | `POP A` | 1 | 3 | Pop into A |
| `25` | `POP Rn` | 2 | 3 | Pop into Rn |
| `26` | `POP FLAGS` | 1 | 3 | Pop into the flags register |

**Arithmetic**

| Opcode | Syntax | Bytes | Cycles | Description |
|---|---|---|---|---|
| `30` | `ADD A, imm8` | 2 | 2 | A = A + imm8 |
| `31` | `ADD A, Rn` | 2 | 1 | A = A + Rn |
| `32` | `ADD A, [addr]` | 2 | 3 | A = A + [addr] |
| `33` | `ADC A, imm8` | 2 | 2 | A = A + imm8 + C |
| `34` | `ADC A, Rn` | 2 | 1 | A = A + Rn + C |
| `35` | `ADC A, [addr]` | 2 | 3 | A = A + [addr] + C |
| `36` | `SUB A, imm8` | 2 | 2 | A = A - imm8 |
| `37` | `SUB A, Rn` | 2 | 1 | A = A - Rn |
| `38` | `SUB A, [addr]` | 2 | 3 | A = A - [addr] |
| `39` | `SBB A, imm8` | 2 | 2 | A = A - imm8 - C |
| `3A` | `SBB A, Rn` | 2 | 1 | A = A - Rn - C |
| `3B` | `SBB A, [addr]` | 2 | 3 | A = A - [addr] - C |
| `3C` | `CMP A, imm8` | 2 | 2 | Set flags from A - imm8 |
| `3D` | `CMP A, Rn` | 2 | 1 | Set flags from A - Rn |
| `3E` | `CMP A, [addr]` | 2 | 3 | Set flags from A - [addr] |
| `40` | `INC A` | 1 | 1 | A = A + 1 (C unchanged) |
| `41` | `INC Rn` | 2 | 1 | Rn = Rn + 1 (C unchanged) |
| `42` | `INC [addr]` | 2 | 4 | [addr] = [addr] + 1 (read-modify-write) |
| `43` | `DEC A` | 1 | 1 | A = A - 1 (C unchanged) |
| `44` | `DEC Rn` | 2 | 1 | Rn = Rn - 1 (C unchanged) |
| `45` | `DEC [addr]` | 2 | 4 | [addr] = [addr] - 1 (read-modify-write) |
| `46` | `MUL Rn` | 2 | 8 | A = low byte of A * Rn, C = V = overflow |
| `47` | `DIV Rn` | 2 | 12 | A = A / Rn (unsigned), faults on zero |
| `48` | `MOD Rn` | 2 | 12 | A = A mod Rn (unsigned), faults on zero |
| `49` | `NEG A` | 1 | 1 | A = -A (two's complement) |

**Logic and shifts**

| Opcode | Syntax | Bytes | Cycles | Description |
|---|---|---|---|---|
| `50` | `AND A, imm8` | 2 | 2 | A = A and imm8 |
| `51` | `AND A, Rn` | 2 | 1 | A = A and Rn |
| `52` | `OR A, imm8` | 2 | 2 | A = A or imm8 |
| `53` | `OR A, Rn` | 2 | 1 | A = A or Rn |
| `54` | `XOR A, imm8` | 2 | 2 | A = A xor imm8 |
| `55` | `XOR A, Rn` | 2 | 1 | A = A xor Rn |
| `56` | `NOT A` | 1 | 1 | A = not A |
| `57` | `SHL A` | 1 | 1 | Shift A left, C = old bit 7 |
| `58` | `SHR A` | 1 | 1 | Shift A right, C = old bit 0 |
| `59` | `ROL A` | 1 | 1 | Rotate A left, C = old bit 7 |
| `5A` | `ROR A` | 1 | 1 | Rotate A right, C = old bit 0 |

**Control flow**

| Opcode | Syntax | Bytes | Cycles | Description |
|---|---|---|---|---|
| `60` | `JMP addr` | 2 | 2 (+1 taken) | Jump |
| `61` | `JZ addr` | 2 | 2 (+1 taken) | Jump if Z = 1 |
| `62` | `JNZ addr` | 2 | 2 (+1 taken) | Jump if Z = 0 |
| `63` | `JC addr` | 2 | 2 (+1 taken) | Jump if C = 1 |
| `64` | `JNC addr` | 2 | 2 (+1 taken) | Jump if C = 0 |
| `65` | `JS addr` | 2 | 2 (+1 taken) | Jump if N = 1 |
| `66` | `JNS addr` | 2 | 2 (+1 taken) | Jump if N = 0 |
| `67` | `JO addr` | 2 | 2 (+1 taken) | Jump if V = 1 |
| `68` | `JNO addr` | 2 | 2 (+1 taken) | Jump if V = 0 |
| `69` | `CALL addr` | 2 | 5 | Push the return address and jump to a subroutine |
| `6A` | `RET` | 1 | 4 | Return from a subroutine |
| `6B` | `LOOP Rn, addr` | 3 | 3 (+1 taken) | Decrement Rn and jump to addr while Rn is not zero |

**Flags**

| Opcode | Syntax | Bytes | Cycles | Description |
|---|---|---|---|---|
| `70` | `CLC` | 1 | 1 | Clear the carry flag |
| `71` | `STC` | 1 | 1 | Set the carry flag |

**Input / output**

| Opcode | Syntax | Bytes | Cycles | Description |
|---|---|---|---|---|
| `F0` | `OUT port, A` | 2 | 3 | Write A to an output port (0 = decimal, 1 = character) |

## Getting started 🛠️

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download) (Visual Studio 2022 17.8 or newer, Rider or the command line).

### Console demo

```bash
dotnet run --project CISCProcessorLibrary/Demo
```

The interactive menu lets you run a sample program, type your own program line by line, or print the instruction set reference. The demo also works without the menu:

```bash
dotnet run --project CISCProcessorLibrary/Demo -- --list                      # sample ids
dotnet run --project CISCProcessorLibrary/Demo -- --sample sort --trace       # run a sample with a trace
dotnet run --project CISCProcessorLibrary/Demo -- program.asm --micro         # run a file, show micro-operations
dotnet run --project CISCProcessorLibrary/Demo -- --sample hello --delay 200  # animate the trace, 200 ms per instruction
```

Example of a traced run:

```
== Execution trace ==
At  Bytes    Instruction         | registers after the instruction (changed = yellow)
00  10 28    MOV A, 40           | A=28 R0=00 R1=00 R2=00 R3=00 R4=00 R5=00 R6=00 R7=00 IP=02 SP=FF ----  2c
02  30 02    ADD A, 2            | A=2A R0=00 R1=00 R2=00 R3=00 R4=00 R5=00 R6=00 R7=00 IP=04 SP=FF ----  2c
04  F0 00    OUT 0, A            | A=2A R0=00 R1=00 R2=00 R3=00 R4=00 R5=00 R6=00 R7=00 IP=06 SP=FF ----  3c
        OUT port 0: 42
06  01       HLT                 | A=2A R0=00 R1=00 R2=00 R3=00 R4=00 R5=00 R6=00 R7=00 IP=07 SP=FF ----  1c

Output: 42
Halted normally after 4 instructions and 8 clock cycles.
```

With `--micro` every bus cycle is shown as well:

```
00  11 00 05 MOV R0, 5           | A=00 R0=05 R1=00 ... IP=03 SP=FF ----  2c
        FETCH  [00] -> IR = 11
        REG    IR 00 -> 11
        REG    IP 00 -> 01
        READ   [01] -> 00 (operand)
        REG    IP 01 -> 02
        READ   [02] -> 05 (operand)
        REG    IP 02 -> 03
        DECODE 11 = MOV R0, 5
        EXEC   MOV
        REG    R0 00 -> 05
```

### Web visualizer 🖥️

Two ways to open it:

1. **Served by .NET** (programs run on the C# core through the API):

   ```bash
   dotnet run --project CISCProcessorLibrary/CISCProcessorWeb
   ```

   then open <http://localhost:5080>. The page detects the server and shows an *Engine* selector (`.NET server` / `JavaScript (browser)`).

2. **Static**: open `web/index.html` directly in a browser, or publish the `web/` folder anywhere (GitHub Pages works). The page then runs on its JavaScript mirror of the processor, which uses the same opcode table, flag rules and cycle counts as the C# library.

What the page shows:

- **Source and listing**: an editor with line numbers and error markers, and the assembler listing (address, bytes, source). Click an address to toggle a **breakpoint**.
- **The die**: control unit with the fetch / decode / execute phases, the decoded instruction, the clock, the register file, `MAR` / `MDR`, an ALU that lights up with its operands and result, and the four flag LEDs.
- **Buses and memory**: packets travel over the address bus (amber) and the data bus (cyan) between the processor and the 16 x 16 memory map. Code, data, the stack and the current instruction are color coded; reads and writes flash.
- **Output ports and execution trace**, plus a status bar with the processor state, `IP`, `SP`, the phase and the engine.
- Controls: *Assemble* (Ctrl+Enter), *Run / Pause* (F5), *Step* (F8), *Reset*, a speed slider from a slow crawl to a turbo mode without animation, the instruction set reference and a light / dark theme. `?program=sort` in the URL preselects a sample.

### Web API

The host exposes the processor as JSON (all paths relative to the site root):

| Method and path | Purpose |
|---|---|
| `GET /api/info` | Engine name, version, runtime, instruction count. |
| `GET /api/instructions` | The opcode table. |
| `GET /api/samples` | The sample programs. |
| `POST /api/assemble` `{ "source": "..." }` | Assembles and returns the listing and symbols, or a `400` with the line and message. |
| `POST /api/sessions` `{ "source": "..." }` | Assembles, creates a processor session and returns its id, program and state. |
| `POST /api/sessions/{id}/step` | Executes one instruction and returns its trace (micro-operations and the state after it). |
| `POST /api/sessions/{id}/run` `{ "maxInstructions": 512, "stopAt": [addresses] }` | Executes up to N instructions, stopping in front of breakpoint addresses. |
| `POST /api/sessions/{id}/reset` | Reloads the program. |
| `DELETE /api/sessions/{id}` | Drops the session. Idle sessions are removed after 30 minutes. |

### Using the library

```csharp
using CISCProcessorLibrary;

var cpu = new CISCProcessor();
cpu.OutputWritten += (_, e) => Console.Write(e.Text + " ");
cpu.InstructionExecuted += (_, step) => Console.Error.WriteLine(step);   // "03  12 00     MOV A, R0   1 cycles"

cpu.LoadProgram(SamplePrograms.Countdown.Source);                        // assembles and loads
RunResult result = cpu.Run();                                            // until HLT, a fault or the budget

Console.WriteLine($"{result.Reason}: {result.InstructionsExecuted} instructions, {result.Cycles} cycles");
Console.WriteLine($"A = {cpu.Accumulator}, flags = {cpu.GetState().FlagsText}");
```

`Assembler.Assemble(source)` returns an `AssembledProgram` with the listing and symbol table, `Disassembler.Disassemble(memory, address)` turns bytes back into text, `cpu.Step()` runs one instruction and returns a `StepTrace` with every `MicroOp`, and `InstructionSet.Default` describes all opcodes.

### Tests

```bash
dotnet test CISCProcessorLibrary
```

## Sample programs 📚

| Id | Name | Shows |
|---|---|---|
| `countdown` | Countdown | `LOOP` and the numeric output port |
| `sum` | Sum of an array | indexed addressing `[data+R1]` over a `.db` table |
| `fibonacci` | Fibonacci | the carry flag stopping a loop at the 8 bit limit |
| `factorial` | Factorial | `CALL` / `RET`, `PUSH` / `POP` and `MUL` |
| `hello` | Hello, CISC! | register indirect addressing `[R0]` and the character port |
| `stack` | Stack and subroutines | swapping registers through the stack, `SHL` in a subroutine |
| `sort` | Bubble sort | nested `LOOP`s and indexed stores that rewrite memory in place |
| `fault` | Hardware fault | `DIV` by zero halting the processor with a fault |

## Repository layout 📁

```
CISCProcessorLibrary/
  CISCProcessorLibrary.sln
  CISCProcessorLibrary/        core library (CISCProcessor, ArithmeticLogicUnit, Memory, InstructionSet,
                               Instructions/, Assembler, Disassembler, SamplePrograms, MicroOp, StepTrace ...)
  Demo/                        console front end
  CISCProcessorWeb/            ASP.NET Core host + JSON API (serves ../../web as wwwroot)
  CISCProcessorLibrary.Tests/  xunit tests
web/
  index.html, css/style.css    the visualizer page
  js/isa.js, cpu.js,           JavaScript mirror of the instruction set, processor and assembler
  js/assembler.js, samples.js
  js/app.js                    user interface and animation player
docs/visualizer.png            screenshot
```

## Changelog 📝

- **0_010** - README for version 0.10.
- **0_009** - Web visualizer (`web/`) with animated buses, ALU, registers, memory map, breakpoints and samples; `CISCProcessorWeb` ASP.NET Core host and JSON API; JavaScript mirror of the core.
- **0_008** - New CISC-8 core: 8 bit registers and memory, hardware stack in memory, Z C N V flags, fetch - decode - execute cycle with cycle counts and micro-operation tracing, 74 instruction opcode table with five addressing modes, two pass assembler, disassembler, faults, sample programs, rewritten console demo (file / sample / interactive modes, traces, memory dump) and an xunit test suite. Replaces the previous `StackProcessor` and the five stand-alone instruction classes.
- **0_007** - Stack processor and colored console output.
- **0_001** - **0_006** - Initial implementation and README.

## License

MIT, see `LICENSE.txt`.
