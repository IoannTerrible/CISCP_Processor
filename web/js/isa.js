/*
 * CISC-16 instruction set - JavaScript mirror of CISCProcessorLibrary/InstructionSet.cs.
 * The opcode table, syntax templates and descriptions are identical to the C# library, so programs
 * assemble to the same bytes and run identically in the browser and on .NET.
 */
(function (global) {
  'use strict';

  // Syntax template tokens -> operand kinds. Mirrors InstructionSyntax.cs.
  const TOKEN_KINDS = {
    'A': 'acc', 'Rn': 'reg', 'Rm': 'reg', 'XR': 'xreg', 'XS': 'xreg',
    'imm8': 'imm', 'port': 'imm', 'imm16': 'imm16', 'addr': 'addr',
    '[addr]': 'direct', '[Rn]': 'indirect', '[addr+Rn]': 'indexed',
    '[XR]': 'xind', '[XS]': 'xind', '[XR+disp8]': 'xdisp', '[XS+disp8]': 'xdisp', 'FLAGS': 'flags',
  };
  const KIND_BYTES = { acc: 0, reg: 1, xreg: 1, imm: 1, imm16: 2, addr: 2, direct: 2, indirect: 1, indexed: 3, xind: 1, xdisp: 2, flags: 0, literal: 0 };
  const NUMERIC_KINDS = new Set(['imm', 'imm16', 'addr']);
  const WIDE_KINDS = new Set(['xreg', 'imm16']);
  const MEMORY_KINDS = new Set(['direct', 'indirect', 'indexed', 'xind', 'xdisp']);
  const ADDRESS_REGISTERS = ['SI', 'DI', 'BP', 'SP'];

  function parseSyntax(template) {
    const space = template.indexOf(' ');
    const mnemonic = (space < 0 ? template : template.slice(0, space)).toUpperCase();
    const operands = [];
    let offset = 0;
    if (space >= 0) {
      for (const raw of template.slice(space + 1).split(',')) {
        const token = raw.trim();
        let kind = TOKEN_KINDS[token];
        let literal = null;
        if (!kind) {
          if (!/^[A-Z][A-Z0-9]*$/.test(token)) throw new Error('Unknown operand token ' + token + ' in ' + template);
          kind = 'literal';
          literal = token;
        }
        operands.push({ kind, offset, bytes: KIND_BYTES[kind], literal });
        offset += KIND_BYTES[kind];
      }
    }
    return { template, mnemonic, operands, operandBytes: offset };
  }

  const hex2 = (v) => (v & 0xFF).toString(16).toUpperCase().padStart(2, '0');
  const hex4 = (v) => (v & 0xFFFF).toString(16).toUpperCase().padStart(4, '0');
  const word = (bytes, o) => bytes[o] | (bytes[o + 1] << 8);
  const signed8 = (v) => (v & 0x80) ? v - 256 : v;

  function formatOperand(slot, bytes) {
    const o = slot.offset;
    switch (slot.kind) {
      case 'acc': return 'A';
      case 'flags': return 'FLAGS';
      case 'literal': return slot.literal;
      case 'reg': return 'R' + (bytes[o] & 7);
      case 'xreg': return ADDRESS_REGISTERS[bytes[o] & 3];
      case 'imm': return String(bytes[o]);
      case 'imm16': return '0x' + hex4(word(bytes, o));
      case 'addr': return '0x' + hex4(word(bytes, o));
      case 'direct': return '[0x' + hex4(word(bytes, o)) + ']';
      case 'indirect': return '[R' + (bytes[o] & 7) + ']';
      case 'indexed': return '[0x' + hex4(word(bytes, o)) + '+R' + (bytes[o + 2] & 7) + ']';
      case 'xind': return '[' + ADDRESS_REGISTERS[bytes[o] & 3] + ']';
      case 'xdisp': {
        const d = signed8(bytes[o + 1]);
        return '[' + ADDRESS_REGISTERS[bytes[o] & 3] + (d >= 0 ? '+' + d : String(d)) + ']';
      }
      default: return '?';
    }
  }

  function format(syntax, operandBytes) {
    if (syntax.operands.length === 0) return syntax.mnemonic;
    return syntax.mnemonic + ' ' + syntax.operands.map((s) => formatOperand(s, operandBytes)).join(', ');
  }

  const byOpcode = new Array(256).fill(null);
  const all = [];

  function define(opcode, template, family, extra, description) {
    if (byOpcode[opcode]) throw new Error('Opcode defined twice: ' + opcode);
    const syntax = parseSyntax(template);
    const instruction = Object.assign({
      opcode, syntax, mnemonic: syntax.mnemonic, family, length: 1 + syntax.operandBytes, description,
      wide: syntax.operands.some((s) => WIDE_KINDS.has(s.kind)),
    }, extra || {});
    byOpcode[opcode] = instruction;
    all.push(instruction);
  }

  // 0x0x - control and interrupts
  define(0x00, 'NOP', 'nop', null, 'No operation');
  define(0x01, 'HLT', 'hlt', null, 'Halt; with interrupts enabled, wait for the next interrupt instead');
  define(0x02, 'EI', 'ei', null, 'Enable maskable interrupts (set I)');
  define(0x03, 'DI', 'di', null, 'Disable maskable interrupts (clear I)');
  define(0x04, 'IRET', 'iret', null, 'Return from an interrupt handler (pop IP, pop FLAGS)');
  define(0x05, 'INT imm8', 'int', null, 'Software interrupt through vector imm8 (0..7)');

  // 0x1x - 8 bit data movement
  define(0x10, 'MOV A, imm8', 'mov', null, 'Load an immediate into A');
  define(0x11, 'MOV Rn, imm8', 'mov', null, 'Load an immediate into Rn');
  define(0x12, 'MOV A, Rn', 'mov', null, 'Copy Rn to A');
  define(0x13, 'MOV Rn, A', 'mov', null, 'Copy A to Rn');
  define(0x14, 'MOV Rn, Rm', 'mov', null, 'Copy Rm to Rn');
  define(0x15, 'MOV A, [addr]', 'mov', null, 'Load A from memory (direct, 16 bit address)');
  define(0x16, 'MOV [addr], A', 'mov', null, 'Store A to memory (direct)');
  define(0x17, 'MOV A, [Rn]', 'mov', null, 'Load A from zero page address Rn (register indirect)');
  define(0x18, 'MOV [Rn], A', 'mov', null, 'Store A to zero page address Rn (register indirect)');
  define(0x19, 'MOV A, [addr+Rn]', 'mov', null, 'Load A from addr + Rn (indexed)');
  define(0x1A, 'MOV [addr+Rn], A', 'mov', null, 'Store A to addr + Rn (indexed)');
  define(0x1B, 'MOV Rn, [addr]', 'mov', null, 'Load Rn from memory (direct)');
  define(0x1C, 'MOV [addr], Rn', 'mov', null, 'Store Rn to memory (direct)');
  define(0x1D, 'MOV A, [XR]', 'mov', null, 'Load A from the address in SI / DI / BP / SP');
  define(0x1E, 'MOV [XR], A', 'mov', null, 'Store A to the address in SI / DI / BP / SP');
  define(0x1F, 'MOV A, [XR+disp8]', 'mov', null, 'Load A from XR + signed displacement (based)');

  // 0x2x - more data movement, 16 bit
  define(0x20, 'MOV [XR+disp8], A', 'mov', null, 'Store A to XR + signed displacement (based)');
  define(0x21, 'XCHG A, Rn', 'xchg', null, 'Swap A and Rn');
  define(0x22, 'MOV XR, imm16', 'mov', null, 'Load a 16 bit immediate into SI / DI / BP / SP');
  define(0x23, 'MOV XR, XS', 'mov', null, 'Copy one address register to another');
  define(0x24, 'MOV XR, [addr]', 'mov', null, 'Load a 16 bit word (little-endian) into an address register');
  define(0x25, 'MOV [addr], XR', 'mov', null, 'Store an address register as a 16 bit word');
  define(0x26, 'LEA XR, [XS+disp8]', 'lea', null, 'XR = XS + disp8 (address arithmetic without a memory access)');
  define(0x27, 'MOV Rn, [XR+disp8]', 'mov', null, 'Load Rn from XR + signed displacement (based)');
  define(0x28, 'MOV [XR+disp8], Rn', 'mov', null, 'Store Rn to XR + signed displacement (based)');
  define(0x29, 'ADD XR, A', 'xalu', { op: 'add' }, 'XR = XR + A (zero extended), for indexing tables');

  // 0x3x - stack and stack frames
  define(0x30, 'PUSH A', 'push', null, 'Push A');
  define(0x31, 'PUSH Rn', 'push', null, 'Push Rn');
  define(0x32, 'PUSH FLAGS', 'push', null, 'Push the flags register');
  define(0x33, 'PUSH XR', 'push', null, 'Push an address register (16 bit)');
  define(0x34, 'POP A', 'pop', null, 'Pop into A');
  define(0x35, 'POP Rn', 'pop', null, 'Pop into Rn');
  define(0x36, 'POP FLAGS', 'pop', null, 'Pop into the flags register');
  define(0x37, 'POP XR', 'pop', null, 'Pop into an address register (16 bit)');
  define(0x38, 'ENTER imm8', 'enter', null, 'Push BP, BP = SP, SP = SP - imm8 (create a stack frame with imm8 bytes of locals)');
  define(0x39, 'LEAVE', 'leave', null, 'SP = BP, pop BP (destroy the stack frame built by ENTER)');

  // 0x4x - 8 bit arithmetic with a second operand
  define(0x40, 'ADD A, imm8', 'alu', { op: 'add' }, 'A = A + imm8');
  define(0x41, 'ADD A, Rn', 'alu', { op: 'add' }, 'A = A + Rn');
  define(0x42, 'ADD A, [addr]', 'alu', { op: 'add' }, 'A = A + [addr]');
  define(0x43, 'ADD A, [XR]', 'alu', { op: 'add' }, 'A = A + [XR]');
  define(0x44, 'ADC A, imm8', 'alu', { op: 'adc' }, 'A = A + imm8 + C');
  define(0x45, 'ADC A, Rn', 'alu', { op: 'adc' }, 'A = A + Rn + C');
  define(0x46, 'ADC A, [addr]', 'alu', { op: 'adc' }, 'A = A + [addr] + C');
  define(0x47, 'SUB A, imm8', 'alu', { op: 'sub' }, 'A = A - imm8');
  define(0x48, 'SUB A, Rn', 'alu', { op: 'sub' }, 'A = A - Rn');
  define(0x49, 'SUB A, [addr]', 'alu', { op: 'sub' }, 'A = A - [addr]');
  define(0x4A, 'SUB A, [XR]', 'alu', { op: 'sub' }, 'A = A - [XR]');
  define(0x4B, 'SBB A, imm8', 'alu', { op: 'sbb' }, 'A = A - imm8 - C');
  define(0x4C, 'SBB A, Rn', 'alu', { op: 'sbb' }, 'A = A - Rn - C');
  define(0x4D, 'SBB A, [addr]', 'alu', { op: 'sbb' }, 'A = A - [addr] - C');
  define(0x4E, 'CMP A, imm8', 'alu', { op: 'cmp' }, 'Set flags from A - imm8');
  define(0x4F, 'CMP A, Rn', 'alu', { op: 'cmp' }, 'Set flags from A - Rn');

  // 0x5x - more arithmetic
  define(0x50, 'CMP A, [addr]', 'alu', { op: 'cmp' }, 'Set flags from A - [addr]');
  define(0x51, 'CMP A, [XR]', 'alu', { op: 'cmp' }, 'Set flags from A - [XR]');
  define(0x52, 'INC A', 'unary', { op: 'inc' }, 'A = A + 1 (C unchanged)');
  define(0x53, 'INC Rn', 'unary', { op: 'inc' }, 'Rn = Rn + 1 (C unchanged)');
  define(0x54, 'INC [addr]', 'unary', { op: 'inc' }, '[addr] = [addr] + 1 (read-modify-write)');
  define(0x55, 'DEC A', 'unary', { op: 'dec' }, 'A = A - 1 (C unchanged)');
  define(0x56, 'DEC Rn', 'unary', { op: 'dec' }, 'Rn = Rn - 1 (C unchanged)');
  define(0x57, 'DEC [addr]', 'unary', { op: 'dec' }, '[addr] = [addr] - 1 (read-modify-write)');
  define(0x58, 'MUL Rn', 'mul', null, 'R7:A = A * Rn (16 bit product, R7 receives the high byte)');
  define(0x59, 'DIV Rn', 'div', null, 'A = R7:A / Rn, R7 = remainder (unsigned); faults on zero or overflow');
  define(0x5A, 'NEG A', 'unary', { op: 'neg' }, "A = -A (two's complement)");
  define(0x5B, 'ADD XR, imm16', 'xalu', { op: 'add' }, 'XR = XR + imm16 (16 bit)');
  define(0x5C, 'SUB XR, imm16', 'xalu', { op: 'sub' }, 'XR = XR - imm16 (16 bit)');
  define(0x5D, 'CMP XR, imm16', 'xalu', { op: 'cmp' }, 'Set flags from XR - imm16 (16 bit)');
  define(0x5E, 'INC XR', 'xalu', { op: 'inc' }, 'XR = XR + 1 (C unchanged)');
  define(0x5F, 'DEC XR', 'xalu', { op: 'dec' }, 'XR = XR - 1 (C unchanged)');

  // 0x6x - logic and shifts
  define(0x60, 'AND A, imm8', 'alu', { op: 'and' }, 'A = A and imm8');
  define(0x61, 'AND A, Rn', 'alu', { op: 'and' }, 'A = A and Rn');
  define(0x62, 'OR A, imm8', 'alu', { op: 'or' }, 'A = A or imm8');
  define(0x63, 'OR A, Rn', 'alu', { op: 'or' }, 'A = A or Rn');
  define(0x64, 'XOR A, imm8', 'alu', { op: 'xor' }, 'A = A xor imm8');
  define(0x65, 'XOR A, Rn', 'alu', { op: 'xor' }, 'A = A xor Rn');
  define(0x66, 'NOT A', 'unary', { op: 'not' }, 'A = not A');
  define(0x67, 'SHL A', 'unary', { op: 'shl' }, 'Shift A left, C = old bit 7');
  define(0x68, 'SHR A', 'unary', { op: 'shr' }, 'Shift A right, C = old bit 0');
  define(0x69, 'ROL A', 'unary', { op: 'rol' }, 'Rotate A left, C = old bit 7');
  define(0x6A, 'ROR A', 'unary', { op: 'ror' }, 'Rotate A right, C = old bit 0');

  // 0x7x - control flow
  define(0x70, 'JMP addr', 'jump', { cond: 'always' }, 'Jump');
  define(0x71, 'JZ addr', 'jump', { cond: 'z' }, 'Jump if Z = 1');
  define(0x72, 'JNZ addr', 'jump', { cond: 'nz' }, 'Jump if Z = 0');
  define(0x73, 'JC addr', 'jump', { cond: 'c' }, 'Jump if C = 1');
  define(0x74, 'JNC addr', 'jump', { cond: 'nc' }, 'Jump if C = 0');
  define(0x75, 'JS addr', 'jump', { cond: 's' }, 'Jump if N = 1');
  define(0x76, 'JNS addr', 'jump', { cond: 'ns' }, 'Jump if N = 0');
  define(0x77, 'JO addr', 'jump', { cond: 'o' }, 'Jump if V = 1');
  define(0x78, 'JNO addr', 'jump', { cond: 'no' }, 'Jump if V = 0');
  define(0x79, 'CALL addr', 'call', null, 'Push the return address and jump to a subroutine');
  define(0x7A, 'RET', 'ret', null, 'Return from a subroutine');
  define(0x7B, 'LOOP Rn, addr', 'loop', null, 'Decrement Rn and jump to addr while Rn is not zero');
  define(0x7C, 'JMP [XR]', 'jmpind', null, 'Jump to the word stored at [XR] (jump tables, computed goto)');

  // 0x8x - flags
  define(0x80, 'CLC', 'flag', { flag: 'c', value: false }, 'Clear the carry flag');
  define(0x81, 'STC', 'flag', { flag: 'c', value: true }, 'Set the carry flag');

  // 0x9x - string operations (SI = source, DI = destination, R6 = count)
  define(0x90, 'LODSB', 'string', { op: 'load', rep: false }, 'A = [SI], SI = SI + 1');
  define(0x91, 'STOSB', 'string', { op: 'store', rep: false }, '[DI] = A, DI = DI + 1');
  define(0x92, 'MOVSB', 'string', { op: 'move', rep: false }, '[DI] = [SI], SI and DI advance');
  define(0x93, 'CMPSB', 'string', { op: 'compare', rep: false }, 'Set flags from [SI] - [DI], SI and DI advance');
  define(0x94, 'SCASB', 'string', { op: 'scan', rep: false }, 'Set flags from A - [DI], DI advances');
  define(0x95, 'REP MOVSB', 'string', { op: 'move', rep: true }, 'Copy R6 bytes from [SI] to [DI]');
  define(0x96, 'REP STOSB', 'string', { op: 'store', rep: true }, 'Fill R6 bytes at [DI] with A');
  define(0x97, 'REP CMPSB', 'string', { op: 'compare', rep: true }, 'Compare up to R6 bytes, stop at the first difference (REPE)');
  define(0x98, 'REP SCASB', 'string', { op: 'scan', rep: true }, 'Scan up to R6 bytes at [DI] for A, stop at the first match (REPNE)');

  // 0xFx - input / output
  define(0xF0, 'OUT port, A', 'out', null, 'Write A to an output port (0 = decimal, 1 = character, 2..4 = timer)');
  define(0xF1, 'IN A, port', 'in', null, 'Read an input port into A (0 / 1 = input queue, 5 = timer ticks, 6 = input status)');

  all.sort((x, y) => x.opcode - y.opcode);

  const byMnemonic = new Map();
  for (const i of all) {
    if (!byMnemonic.has(i.mnemonic)) byMnemonic.set(i.mnemonic, []);
    byMnemonic.get(i.mnemonic).push(i);
  }

  /** Decodes the instruction at `address` in a 64 KB memory array (mirror of Disassembler.cs). */
  function disassemble(memory, address) {
    address &= 0xFFFF;
    const opcode = memory[address];
    const instruction = byOpcode[opcode];
    if (!instruction) return { address, bytes: [opcode], text: '.db 0x' + hex2(opcode), instruction: null, length: 1 };
    const bytes = [opcode];
    for (let i = 0; i < instruction.syntax.operandBytes; i++) bytes.push(memory[(address + 1 + i) & 0xFFFF]);
    return { address, bytes, text: format(instruction.syntax, bytes.slice(1)), instruction, length: bytes.length };
  }

  global.CISC = global.CISC || {};
  global.CISC.ISA = {
    all, byOpcode, decode: (opcode) => byOpcode[opcode & 0xFF] || null,
    withMnemonic: (m) => byMnemonic.get(String(m).toUpperCase()) || [],
    mnemonics: () => Array.from(byMnemonic.keys()),
    isNumericKind: (kind) => NUMERIC_KINDS.has(kind),
    isWideKind: (kind) => WIDE_KINDS.has(kind),
    isMemoryKind: (kind) => MEMORY_KINDS.has(kind),
    ADDRESS_REGISTERS, format, parseSyntax, disassemble, hex2, hex4, signed8,
  };
})(typeof window !== 'undefined' ? window : globalThis);
