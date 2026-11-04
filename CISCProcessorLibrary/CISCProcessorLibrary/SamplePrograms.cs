namespace CISCProcessorLibrary;

/// <summary>A ready to run assembly program shipped with the library.</summary>
/// <param name="Input">Text queued into the input device before the program runs (null for none).</param>
public sealed record SampleProgram(string Id, string Name, string Description, string Source, string? Input = null);

/// <summary>Example programs that show off the instruction set. The web visualiser ships the same sources.</summary>
public static class SamplePrograms
{
    public static readonly SampleProgram Countdown = new(
        "countdown",
        "Countdown",
        "Counts down from 5 to 1 with a single LOOP instruction and prints every value.",
        """
        ; Count down from 5 to 1 and print every value on port 0.
        ; LOOP decrements R0 and jumps back while it is not zero.
                MOV R0, 5          ; loop counter
        again:  MOV A, R0
                OUT 0, A           ; port 0 prints numbers
                LOOP R0, again     ; R0 = R0 - 1, jump while R0 != 0
                HLT
        """);

    public static readonly SampleProgram SumArray = new(
        "sum",
        "Sum of an array",
        "Adds six bytes stored after the code using the indexed addressing mode [data+R1].",
        """
        ; Add the bytes stored at 'data' and print the sum.
        ; [data+R1] is the indexed addressing mode: address = data + R1.
                MOV R0, 6           ; element count
                MOV R1, 0           ; index
                MOV R2, 0           ; running sum
        next:   MOV A, [data+R1]    ; A = data[R1]
                ADD A, R2
                MOV R2, A
                INC R1
                LOOP R0, next
                MOV A, R2
                OUT 0, A            ; prints 210
                HLT
        data:   .db 10, 20, 30, 40, 50, 60
        """);

    public static readonly SampleProgram Fibonacci = new(
        "fibonacci",
        "Fibonacci",
        "Prints the Fibonacci sequence until the next value overflows 8 bits (the carry flag stops the loop).",
        """
        ; Print Fibonacci numbers until the next one no longer fits in a byte.
                MOV R0, 0           ; previous value
                MOV R1, 1           ; current value
        loop:   MOV A, R1
                OUT 0, A
                ADD A, R0           ; A = current + previous
                JC done             ; carry set: the sum passed 255
                MOV R0, R1
                MOV R1, A
                JMP loop
        done:   HLT
        """);

    public static readonly SampleProgram Factorial = new(
        "factorial",
        "Factorial (CALL / RET / MUL)",
        "Computes 5! inside a subroutine that keeps its working register on the stack.",
        """
        ; Compute 5! = 120 with a subroutine and the MUL instruction.
        ; MUL leaves the high byte of the product in R7, like AH on the 8086.
                MOV R0, 5
                CALL factorial
                OUT 0, A            ; prints 120
                HLT

        factorial:                  ; A = R0!, R0 is preserved
                PUSH R0
                MOV A, 1
        again:  MUL R0              ; R7:A = A * R0
                DEC R0
                JNZ again
                POP R0
                RET
        """);

    public static readonly SampleProgram Hello = new(
        "hello",
        "Hello, CISC!",
        "Walks a zero terminated string with the string instruction LODSB and prints it on the character port.",
        """
        ; Print a zero terminated string character by character.
        ; LODSB loads A from [SI] and advances SI: one instruction instead of three.
                MOV SI, text        ; SI points at the first character
        next:   LODSB               ; A = [SI], SI = SI + 1
                CMP A, 0
                JZ done             ; stop at the terminator
                OUT 1, A            ; port 1 prints characters
                JMP next
        done:   HLT
        text:   .db "Hello, CISC!", 0
        """);

    public static readonly SampleProgram Stack = new(
        "stack",
        "Stack and subroutines",
        "Swaps two registers through the stack and doubles values in a subroutine.",
        """
        ; Swap R0 and R1 through the stack, then double both in a subroutine.
                MOV R0, 3
                MOV R1, 7
                PUSH R0
                PUSH R1
                POP R0              ; R0 = 7
                POP R1              ; R1 = 3
                MOV A, R0
                CALL double
                OUT 0, A            ; prints 14
                MOV A, R1
                CALL double
                OUT 0, A            ; prints 6
                HLT

        double: SHL A               ; A = A * 2, C = old bit 7
                RET
        """);

    public static readonly SampleProgram BubbleSort = new(
        "sort",
        "Bubble sort",
        "Sorts six bytes in place with nested LOOPs and indexed stores, then prints them. Try the three micro-architectures on it.",
        """
        ; Sort the bytes at 'list' in place (bubble sort) and print them.
                MOV R0, 5           ; passes = n - 1
        outer:  MOV R1, 0           ; i
                MOV R2, 5           ; comparisons per pass
        inner:  MOV A, [list+R1]
                MOV R3, A           ; R3 = list[i]
                INC R1
                MOV A, [list+R1]    ; A = list[i+1]
                CMP A, R3
                JNC skip            ; already in order
                DEC R1
                MOV [list+R1], A    ; list[i] = smaller
                INC R1
                MOV A, R3
                MOV [list+R1], A    ; list[i+1] = larger
        skip:   LOOP R2, inner
                LOOP R0, outer

                MOV R1, 0           ; print the sorted list
                MOV R0, 6
        print:  MOV A, [list+R1]
                OUT 0, A
                INC R1
                LOOP R0, print
                HLT
        list:   .db 42, 7, 19, 3, 88, 25
        """);

    public static readonly SampleProgram Strings = new(
        "strings",
        "String instructions (REP MOVSB)",
        "Copies a string with one REP MOVSB and prints the copy with a LODSB loop.",
        """
        ; Copy a string with a single REP MOVSB, then print the copy.
        ; SI = source, DI = destination, R6 = byte count (the CX of this machine).
                MOV SI, source
                MOV DI, buffer
                MOV R6, 14          ; 13 characters plus the terminator
                REP MOVSB           ; the whole copy in one instruction
                MOV SI, buffer
        next:   LODSB
                CMP A, 0
                JZ done
                OUT 1, A
                JMP next
        done:   HLT
        source: .db "String ops OK", 0
        buffer: .ds 14
        """);

    public static readonly SampleProgram Scan = new(
        "scan",
        "Fill and scan (REP STOSB / SCASB)",
        "Fills a buffer with REP STOSB, plants a marker and finds it again with REP SCASB.",
        """
        ; Fill 16 bytes with '-', put a '*' at index 9 and find it with REP SCASB.
                MOV DI, buffer
                MOV A, '-'
                MOV R6, 16
                REP STOSB           ; fill the buffer
                MOV A, '*'
                MOV R1, 9
                MOV [buffer+R1], A  ; plant the marker
                MOV DI, buffer
                MOV R6, 16
                REP SCASB           ; stop after the first byte equal to A
                MOV A, 16           ; index = 16 - R6 - 1
                SUB A, R6
                DEC A
                OUT 0, A            ; prints 9
                HLT
        buffer: .ds 16
        """);

    public static readonly SampleProgram Frames = new(
        "frames",
        "Stack frames (ENTER / LEAVE / [BP+disp])",
        "Calls a function with two stack arguments and a local variable, the way a compiler would.",
        """
        ; A C style call: arguments on the stack, ENTER builds the frame, BP addresses locals and arguments.
                MOV A, 30
                PUSH A              ; second argument
                MOV A, 12
                PUSH A              ; first argument
                CALL add2
                ADD SP, 2           ; the caller removes the arguments
                OUT 0, A            ; prints 42
                HLT

        add2:   ENTER 1             ; push BP, BP = SP, one byte of locals
                MOV A, [BP+4]       ; first argument  (BP+0..1 = old BP, BP+2..3 = return address)
                MOV [BP-1], A       ; keep it in the local variable
                MOV A, [BP+5]       ; second argument
                MOV R1, [BP-1]      ; fetch the local
                ADD A, R1
                LEAVE               ; SP = BP, pop BP
                RET
        """);

    public static readonly SampleProgram JumpTable = new(
        "jumptable",
        "Jump table (JMP [SI])",
        "Dispatches through a table of 16 bit addresses with an indirect jump, the computed goto of CISC machines.",
        """
        ; Dispatch through a jump table: JMP [SI] jumps to the 16 bit address stored at SI.
                MOV R0, 1           ; which routine to run: 0, 1 or 2
                MOV A, R0
                SHL A               ; entries are 2 bytes wide
                MOV SI, table
                ADD SI, A           ; SI = table + 2 * R0
                JMP [SI]            ; computed goto
        zero:   MOV A, 'Z'
                JMP show
        one:    MOV A, 'O'
                JMP show
        two:    MOV A, 'T'
        show:   OUT 1, A
                HLT
        table:  .dw zero, one, two
        """);

    public static readonly SampleProgram Interrupt = new(
        "interrupt",
        "Timer interrupt",
        "A programmable timer raises IRQ 0; the handler counts ticks while the main program sleeps in HLT.",
        """
        ; The timer raises IRQ 0 every 200 clock cycles. The handler counts the ticks and prints them;
        ; the main program sleeps in HLT between interrupts and stops for good after the fifth tick.
                MOV A, 200          ; timer period, low byte
                OUT 2, A
                MOV A, 0            ; timer period, high byte
                OUT 3, A
                MOV A, 1            ; start the timer
                OUT 4, A
                MOV R0, 0           ; tick counter
                EI                  ; allow interrupts
        wait:   HLT                 ; sleep until the next interrupt
                MOV A, R0
                CMP A, 5
                JNZ wait
                DI
                HLT                 ; interrupts disabled: this one really stops

        handler:                    ; IRQ 0 service routine
                PUSH A
                INC R0
                MOV A, R0
                OUT 0, A            ; prints 1 2 3 4 5
                POP A
                IRET

                .org 0xFFF0         ; interrupt vector table
                .dw handler         ; IRQ 0 -> handler
        """);

    public static readonly SampleProgram Echo = new(
        "echo",
        "Input echo (IN)",
        "Reads the input device until it is empty and echoes every character in upper case.",
        """
        ; Read characters from the input device until the queue is empty and echo them in upper case.
        ; Port 6 reports whether a byte is waiting, port 1 delivers the next character.
        next:   IN A, 6             ; input status: bit 0 = a byte is waiting
                AND A, 1
                JZ done
                IN A, 1             ; next character
                CMP A, 'a'
                JC echo             ; below 'a': leave it
                CMP A, 'z'+1
                JNC echo            ; above 'z': leave it
                SUB A, 32           ; to upper case
        echo:   OUT 1, A
                JMP next
        done:   HLT
        """,
        "hello, cisc-16!");

    public static readonly SampleProgram Primes = new(
        "primes",
        "Prime numbers (long run)",
        "Prints every prime below 256 by counting divisors: about 200 thousand instructions, a few seconds at full speed and a benchmark for the three eras.",
        """
        ; Print every prime below 256. For each candidate n the program counts the divisors
        ; between 2 and n / 2; n is prime when the count stays zero.
        ; About 200 thousand instructions: a long run for the full speed mode.
                MOV R0, 2           ; candidate n
        next:   MOV A, R0
                SHR A
                MOV R3, A           ; limit = n / 2
                MOV R2, 0           ; divisor count
                MOV R1, 2           ; divisor d
        trial:  MOV A, R1
                CMP A, R3
                JC test             ; d < limit
                JZ test             ; d == limit
                JMP done            ; d > limit: all divisors tried
        test:   MOV R7, 0           ; dividend R7:A = n
                MOV A, R0
                DIV R1              ; R7 = n mod d
                MOV A, R7
                CMP A, 0
                JNZ skip
                INC R2              ; d divides n
        skip:   INC R1
                JMP trial
        done:   MOV A, R2
                CMP A, 0
                JNZ composite       ; at least one divisor
                MOV A, R0
                OUT 0, A            ; n is prime
        composite:
                INC R0
                JNZ next            ; R0 wraps to 0 after 255: all candidates done
                HLT
        """);

    public static readonly SampleProgram DivideByZero = new(
        "fault",
        "Hardware fault",
        "Divides by a zero register so the processor stops with a DivideByZero fault.",
        """
        ; DIV by zero raises a hardware fault that halts the processor.
                MOV A, 100
                MOV R0, 0
                DIV R0              ; fault: divide by zero
                HLT                 ; never reached
        """);

    /// <summary>All sample programs in menu order.</summary>
    public static IReadOnlyList<SampleProgram> All { get; } = new[]
    {
        Countdown, SumArray, Fibonacci, Factorial, Hello, Stack, BubbleSort,
        Strings, Scan, Frames, JumpTable, Interrupt, Echo, Primes, DivideByZero,
    };

    /// <summary>Finds a sample by its id (case-insensitive).</summary>
    public static SampleProgram? Find(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
