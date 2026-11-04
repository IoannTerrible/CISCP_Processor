/* Sample programs - generated from CISCProcessorLibrary/SamplePrograms.cs so both front ends ship the same sources. */
(function (global) {
  'use strict';

  const SAMPLES = [
  {
    "id": "countdown",
    "name": "Countdown",
    "description": "Counts down from 5 to 1 with a single LOOP instruction and prints every value.",
    "source": "; Count down from 5 to 1 and print every value on port 0.\n; LOOP decrements R0 and jumps back while it is not zero.\n        MOV R0, 5          ; loop counter\nagain:  MOV A, R0\n        OUT 0, A           ; port 0 prints numbers\n        LOOP R0, again     ; R0 = R0 - 1, jump while R0 != 0\n        HLT",
    "input": null
  },
  {
    "id": "sum",
    "name": "Sum of an array",
    "description": "Adds six bytes stored after the code using the indexed addressing mode [data+R1].",
    "source": "; Add the bytes stored at 'data' and print the sum.\n; [data+R1] is the indexed addressing mode: address = data + R1.\n        MOV R0, 6           ; element count\n        MOV R1, 0           ; index\n        MOV R2, 0           ; running sum\nnext:   MOV A, [data+R1]    ; A = data[R1]\n        ADD A, R2\n        MOV R2, A\n        INC R1\n        LOOP R0, next\n        MOV A, R2\n        OUT 0, A            ; prints 210\n        HLT\ndata:   .db 10, 20, 30, 40, 50, 60",
    "input": null
  },
  {
    "id": "fibonacci",
    "name": "Fibonacci",
    "description": "Prints the Fibonacci sequence until the next value overflows 8 bits (the carry flag stops the loop).",
    "source": "; Print Fibonacci numbers until the next one no longer fits in a byte.\n        MOV R0, 0           ; previous value\n        MOV R1, 1           ; current value\nloop:   MOV A, R1\n        OUT 0, A\n        ADD A, R0           ; A = current + previous\n        JC done             ; carry set: the sum passed 255\n        MOV R0, R1\n        MOV R1, A\n        JMP loop\ndone:   HLT",
    "input": null
  },
  {
    "id": "factorial",
    "name": "Factorial (CALL / RET / MUL)",
    "description": "Computes 5! inside a subroutine that keeps its working register on the stack.",
    "source": "; Compute 5! = 120 with a subroutine and the MUL instruction.\n; MUL leaves the high byte of the product in R7, like AH on the 8086.\n        MOV R0, 5\n        CALL factorial\n        OUT 0, A            ; prints 120\n        HLT\n\nfactorial:                  ; A = R0!, R0 is preserved\n        PUSH R0\n        MOV A, 1\nagain:  MUL R0              ; R7:A = A * R0\n        DEC R0\n        JNZ again\n        POP R0\n        RET",
    "input": null
  },
  {
    "id": "hello",
    "name": "Hello, CISC!",
    "description": "Walks a zero terminated string with the string instruction LODSB and prints it on the character port.",
    "source": "; Print a zero terminated string character by character.\n; LODSB loads A from [SI] and advances SI: one instruction instead of three.\n        MOV SI, text        ; SI points at the first character\nnext:   LODSB               ; A = [SI], SI = SI + 1\n        CMP A, 0\n        JZ done             ; stop at the terminator\n        OUT 1, A            ; port 1 prints characters\n        JMP next\ndone:   HLT\ntext:   .db \"Hello, CISC!\", 0",
    "input": null
  },
  {
    "id": "stack",
    "name": "Stack and subroutines",
    "description": "Swaps two registers through the stack and doubles values in a subroutine.",
    "source": "; Swap R0 and R1 through the stack, then double both in a subroutine.\n        MOV R0, 3\n        MOV R1, 7\n        PUSH R0\n        PUSH R1\n        POP R0              ; R0 = 7\n        POP R1              ; R1 = 3\n        MOV A, R0\n        CALL double\n        OUT 0, A            ; prints 14\n        MOV A, R1\n        CALL double\n        OUT 0, A            ; prints 6\n        HLT\n\ndouble: SHL A               ; A = A * 2, C = old bit 7\n        RET",
    "input": null
  },
  {
    "id": "sort",
    "name": "Bubble sort",
    "description": "Sorts six bytes in place with nested LOOPs and indexed stores, then prints them. Try the three micro-architectures on it.",
    "source": "; Sort the bytes at 'list' in place (bubble sort) and print them.\n        MOV R0, 5           ; passes = n - 1\nouter:  MOV R1, 0           ; i\n        MOV R2, 5           ; comparisons per pass\ninner:  MOV A, [list+R1]\n        MOV R3, A           ; R3 = list[i]\n        INC R1\n        MOV A, [list+R1]    ; A = list[i+1]\n        CMP A, R3\n        JNC skip            ; already in order\n        DEC R1\n        MOV [list+R1], A    ; list[i] = smaller\n        INC R1\n        MOV A, R3\n        MOV [list+R1], A    ; list[i+1] = larger\nskip:   LOOP R2, inner\n        LOOP R0, outer\n\n        MOV R1, 0           ; print the sorted list\n        MOV R0, 6\nprint:  MOV A, [list+R1]\n        OUT 0, A\n        INC R1\n        LOOP R0, print\n        HLT\nlist:   .db 42, 7, 19, 3, 88, 25",
    "input": null
  },
  {
    "id": "strings",
    "name": "String instructions (REP MOVSB)",
    "description": "Copies a string with one REP MOVSB and prints the copy with a LODSB loop.",
    "source": "; Copy a string with a single REP MOVSB, then print the copy.\n; SI = source, DI = destination, R6 = byte count (the CX of this machine).\n        MOV SI, source\n        MOV DI, buffer\n        MOV R6, 14          ; 13 characters plus the terminator\n        REP MOVSB           ; the whole copy in one instruction\n        MOV SI, buffer\nnext:   LODSB\n        CMP A, 0\n        JZ done\n        OUT 1, A\n        JMP next\ndone:   HLT\nsource: .db \"String ops OK\", 0\nbuffer: .ds 14",
    "input": null
  },
  {
    "id": "scan",
    "name": "Fill and scan (REP STOSB / SCASB)",
    "description": "Fills a buffer with REP STOSB, plants a marker and finds it again with REP SCASB.",
    "source": "; Fill 16 bytes with '-', put a '*' at index 9 and find it with REP SCASB.\n        MOV DI, buffer\n        MOV A, '-'\n        MOV R6, 16\n        REP STOSB           ; fill the buffer\n        MOV A, '*'\n        MOV R1, 9\n        MOV [buffer+R1], A  ; plant the marker\n        MOV DI, buffer\n        MOV R6, 16\n        REP SCASB           ; stop after the first byte equal to A\n        MOV A, 16           ; index = 16 - R6 - 1\n        SUB A, R6\n        DEC A\n        OUT 0, A            ; prints 9\n        HLT\nbuffer: .ds 16",
    "input": null
  },
  {
    "id": "frames",
    "name": "Stack frames (ENTER / LEAVE / [BP+disp])",
    "description": "Calls a function with two stack arguments and a local variable, the way a compiler would.",
    "source": "; A C style call: arguments on the stack, ENTER builds the frame, BP addresses locals and arguments.\n        MOV A, 30\n        PUSH A              ; second argument\n        MOV A, 12\n        PUSH A              ; first argument\n        CALL add2\n        ADD SP, 2           ; the caller removes the arguments\n        OUT 0, A            ; prints 42\n        HLT\n\nadd2:   ENTER 1             ; push BP, BP = SP, one byte of locals\n        MOV A, [BP+4]       ; first argument  (BP+0..1 = old BP, BP+2..3 = return address)\n        MOV [BP-1], A       ; keep it in the local variable\n        MOV A, [BP+5]       ; second argument\n        MOV R1, [BP-1]      ; fetch the local\n        ADD A, R1\n        LEAVE               ; SP = BP, pop BP\n        RET",
    "input": null
  },
  {
    "id": "jumptable",
    "name": "Jump table (JMP [SI])",
    "description": "Dispatches through a table of 16 bit addresses with an indirect jump, the computed goto of CISC machines.",
    "source": "; Dispatch through a jump table: JMP [SI] jumps to the 16 bit address stored at SI.\n        MOV R0, 1           ; which routine to run: 0, 1 or 2\n        MOV A, R0\n        SHL A               ; entries are 2 bytes wide\n        MOV SI, table\n        ADD SI, A           ; SI = table + 2 * R0\n        JMP [SI]            ; computed goto\nzero:   MOV A, 'Z'\n        JMP show\none:    MOV A, 'O'\n        JMP show\ntwo:    MOV A, 'T'\nshow:   OUT 1, A\n        HLT\ntable:  .dw zero, one, two",
    "input": null
  },
  {
    "id": "interrupt",
    "name": "Timer interrupt",
    "description": "A programmable timer raises IRQ 0; the handler counts ticks while the main program sleeps in HLT.",
    "source": "; The timer raises IRQ 0 every 200 clock cycles. The handler counts the ticks and prints them;\n; the main program sleeps in HLT between interrupts and stops for good after the fifth tick.\n        MOV A, 200          ; timer period, low byte\n        OUT 2, A\n        MOV A, 0            ; timer period, high byte\n        OUT 3, A\n        MOV A, 1            ; start the timer\n        OUT 4, A\n        MOV R0, 0           ; tick counter\n        EI                  ; allow interrupts\nwait:   HLT                 ; sleep until the next interrupt\n        MOV A, R0\n        CMP A, 5\n        JNZ wait\n        DI\n        HLT                 ; interrupts disabled: this one really stops\n\nhandler:                    ; IRQ 0 service routine\n        PUSH A\n        INC R0\n        MOV A, R0\n        OUT 0, A            ; prints 1 2 3 4 5\n        POP A\n        IRET\n\n        .org 0xFFF0         ; interrupt vector table\n        .dw handler         ; IRQ 0 -> handler",
    "input": null
  },
  {
    "id": "echo",
    "name": "Input echo (IN)",
    "description": "Reads the input device until it is empty and echoes every character in upper case.",
    "source": "; Read characters from the input device until the queue is empty and echo them in upper case.\n; Port 6 reports whether a byte is waiting, port 1 delivers the next character.\nnext:   IN A, 6             ; input status: bit 0 = a byte is waiting\n        AND A, 1\n        JZ done\n        IN A, 1             ; next character\n        CMP A, 'a'\n        JC echo             ; below 'a': leave it\n        CMP A, 'z'+1\n        JNC echo            ; above 'z': leave it\n        SUB A, 32           ; to upper case\necho:   OUT 1, A\n        JMP next\ndone:   HLT",
    "input": "hello, cisc-16!"
  },
  {
    "id": "primes",
    "name": "Prime numbers (long run)",
    "description": "Prints every prime below 256 by counting divisors: about 200 thousand instructions, a few seconds at full speed and a benchmark for the three eras.",
    "source": "; Print every prime below 256. For each candidate n the program counts the divisors\n; between 2 and n / 2; n is prime when the count stays zero.\n; About 200 thousand instructions: a long run for the full speed mode.\n        MOV R0, 2           ; candidate n\nnext:   MOV A, R0\n        SHR A\n        MOV R3, A           ; limit = n / 2\n        MOV R2, 0           ; divisor count\n        MOV R1, 2           ; divisor d\ntrial:  MOV A, R1\n        CMP A, R3\n        JC test             ; d < limit\n        JZ test             ; d == limit\n        JMP done            ; d > limit: all divisors tried\ntest:   MOV R7, 0           ; dividend R7:A = n\n        MOV A, R0\n        DIV R1              ; R7 = n mod d\n        MOV A, R7\n        CMP A, 0\n        JNZ skip\n        INC R2              ; d divides n\nskip:   INC R1\n        JMP trial\ndone:   MOV A, R2\n        CMP A, 0\n        JNZ composite       ; at least one divisor\n        MOV A, R0\n        OUT 0, A            ; n is prime\ncomposite:\n        INC R0\n        JNZ next            ; R0 wraps to 0 after 255: all candidates done\n        HLT",
    "input": null
  },
  {
    "id": "fault",
    "name": "Hardware fault",
    "description": "Divides by a zero register so the processor stops with a DivideByZero fault.",
    "source": "; DIV by zero raises a hardware fault that halts the processor.\n        MOV A, 100\n        MOV R0, 0\n        DIV R0              ; fault: divide by zero\n        HLT                 ; never reached",
    "input": null
  }
];

  global.CISC = global.CISC || {};
  global.CISC.SAMPLES = SAMPLES;
})(typeof window !== 'undefined' ? window : globalThis);
