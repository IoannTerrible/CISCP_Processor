namespace CISCProcessorLibrary;

/// <summary>A ready to run assembly program shipped with the library.</summary>
public sealed record SampleProgram(string Id, string Name, string Description, string Source);

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
                MOV R0, 5
                CALL factorial
                OUT 0, A            ; prints 120
                HLT

        factorial:                  ; A = R0!, R0 is preserved
                PUSH R0
                MOV A, 1
        again:  MUL R0              ; A = A * R0
                DEC R0
                JNZ again
                POP R0
                RET
        """);

    public static readonly SampleProgram Hello = new(
        "hello",
        "Hello, CISC!",
        "Walks a zero terminated string with register indirect addressing and prints it on the character port.",
        """
        ; Print a zero terminated string character by character.
        ; [R0] is register indirect addressing: the address is held in R0.
                MOV R0, text        ; R0 points at the first character
        next:   MOV A, [R0]
                CMP A, 0
                JZ done             ; stop at the terminator
                OUT 1, A            ; port 1 prints characters
                INC R0
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
        "Sorts six bytes in place with nested LOOPs and indexed stores, then prints them.",
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
        Countdown, SumArray, Fibonacci, Factorial, Hello, Stack, BubbleSort, DivideByZero,
    };

    /// <summary>Finds a sample by its id (case-insensitive).</summary>
    public static SampleProgram? Find(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
