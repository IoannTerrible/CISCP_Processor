/* Sample programs - the same sources as CISCProcessorLibrary/SamplePrograms.cs. */
(function (global) {
  'use strict';

  const SAMPLES = [
    {
      id: 'countdown', name: 'Countdown',
      description: 'Counts down from 5 to 1 with a single LOOP instruction and prints every value.',
      source: `; Count down from 5 to 1 and print every value on port 0.
; LOOP decrements R0 and jumps back while it is not zero.
        MOV R0, 5          ; loop counter
again:  MOV A, R0
        OUT 0, A           ; port 0 prints numbers
        LOOP R0, again     ; R0 = R0 - 1, jump while R0 != 0
        HLT`,
    },
    {
      id: 'sum', name: 'Sum of an array',
      description: 'Adds six bytes stored after the code using the indexed addressing mode [data+R1].',
      source: `; Add the bytes stored at 'data' and print the sum.
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
data:   .db 10, 20, 30, 40, 50, 60`,
    },
    {
      id: 'fibonacci', name: 'Fibonacci',
      description: 'Prints the Fibonacci sequence until the next value overflows 8 bits (the carry flag stops the loop).',
      source: `; Print Fibonacci numbers until the next one no longer fits in a byte.
        MOV R0, 0           ; previous value
        MOV R1, 1           ; current value
loop:   MOV A, R1
        OUT 0, A
        ADD A, R0           ; A = current + previous
        JC done             ; carry set: the sum passed 255
        MOV R0, R1
        MOV R1, A
        JMP loop
done:   HLT`,
    },
    {
      id: 'factorial', name: 'Factorial (CALL / RET / MUL)',
      description: 'Computes 5! inside a subroutine that keeps its working register on the stack.',
      source: `; Compute 5! = 120 with a subroutine and the MUL instruction.
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
        RET`,
    },
    {
      id: 'hello', name: 'Hello, CISC!',
      description: 'Walks a zero terminated string with register indirect addressing and prints it on the character port.',
      source: `; Print a zero terminated string character by character.
; [R0] is register indirect addressing: the address is held in R0.
        MOV R0, text        ; R0 points at the first character
next:   MOV A, [R0]
        CMP A, 0
        JZ done             ; stop at the terminator
        OUT 1, A            ; port 1 prints characters
        INC R0
        JMP next
done:   HLT
text:   .db "Hello, CISC!", 0`,
    },
    {
      id: 'stack', name: 'Stack and subroutines',
      description: 'Swaps two registers through the stack and doubles values in a subroutine.',
      source: `; Swap R0 and R1 through the stack, then double both in a subroutine.
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
        RET`,
    },
    {
      id: 'sort', name: 'Bubble sort',
      description: 'Sorts six bytes in place with nested LOOPs and indexed stores, then prints them.',
      source: `; Sort the bytes at 'list' in place (bubble sort) and print them.
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
list:   .db 42, 7, 19, 3, 88, 25`,
    },
    {
      id: 'fault', name: 'Hardware fault',
      description: 'Divides by a zero register so the processor stops with a DivideByZero fault.',
      source: `; DIV by zero raises a hardware fault that halts the processor.
        MOV A, 100
        MOV R0, 0
        DIV R0              ; fault: divide by zero
        HLT                 ; never reached`,
    },
  ];

  global.CISC = global.CISC || {};
  global.CISC.SAMPLES = SAMPLES;
})(typeof window !== 'undefined' ? window : globalThis);
