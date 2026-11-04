/*
 * CISC-8 instruction set - JavaScript mirror of CISCProcessorLibrary/InstructionSet.cs.
 * The opcode table, syntax templates, cycle counts and descriptions are identical to the C# library,
 * so programs assemble to the same bytes and run with the same timing in the browser and on .NET.
 */
(function (global) {
  'use strict';

  // Syntax template tokens -> operand kinds. Mirrors InstructionSyntax.cs.
  const TOKEN_KINDS = {
    'A': 'acc', 'Rn': 'reg', 'Rm': 'reg', 'imm8': 'imm', 'port': 'imm', 'addr': 'addr',
    '[addr]': 'direct', '[Rn]': 'indirect', '[addr+Rn]': 'indexed', 'SP': 'sp', 'FLAGS': 'flags',
  };
  const KIND_BYTES = { acc: 0, reg: 1, imm: 1, addr: 1, direct: 1, indirect: 1, indexed: 2, sp: 0, flags: 0 };
  const NUMERIC_KINDS = new Set(['imm', 'addr']);

  function parseSyntax(template) {
    const space = template.indexOf(' ');
    const mnemonic = (space < 0 ? template : template.slice(0, space)).toUpperCase();
    const operands = [];
    let offset = 0;
    if (space >= 0) {
      for (const raw of template.slice(space + 1).split(',')) {
        const token = raw.trim();
        const kind = TOKEN_KINDS[token];
        if (!kind) throw new Error('Unknown operand token ' + token + ' in ' + template);
        operands.push({ kind, offset, bytes: KIND_BYTES[kind] });
        offset += KIND_BYTES[kind];
      }
    }
    return { template, mnemonic, operands, operandBytes: offset };
  }

  const hex2 = (v) => v.toString(16).toUpperCase().padStart(2, '0');

  function formatOperand(slot, bytes) {
    switch (slot.kind) {
      case 'acc': return 'A';
      case 'sp': return 'SP';
      case 'flags': return 'FLAGS';
      case 'reg': return 'R' + (bytes[slot.offset] & 7);
      case 'imm': return String(bytes[slot.offset]);
      case 'addr': return '0x' + hex2(bytes[slot.offset]);
      case 'direct': return '[0x' + hex2(bytes[slot.offset]) + ']';
      case 'indirect': return '[R' + (bytes[slot.offset] & 7) + ']';
      case 'indexed': return '[0x' + hex2(bytes[slot.offset]) + '+R' + (bytes[slot.offset + 1] & 7) + ']';
      default: return '?';
    }
  }

  function format(syntax, operandBytes) {
    if (syntax.operands.length === 0) return syntax.mnemonic;
    return syntax.mnemonic + ' ' + syntax.operands.map((s) => formatOperand(s, operandBytes)).join(', ');
  }

  const byOpcode = new Array(256).fill(null);
  const all = [];

  function define(opcode, template, cycles, family, extra, description) {
    if (byOpcode[opcode]) throw new Error('Opcode defined twice: ' + opcode);
    const syntax = parseSyntax(template);
    const instruction = Object.assign({
      opcode, syntax, mnemonic: syntax.mnemonic, cycles, family,
      length: 1 + syntax.operandBytes, description,
    }, extra || {});
    byOpcode[opcode] = instruction;
    all.push(instruction);
  }

  // 0x0x - control
  define(0x00, 'NOP', 1, 'nop', null, 'No operation');
  define(0x01, 'HLT', 1, 'hlt', null, 'Halt the processor');

  // 0x1x - data movement
  define(0x10, 'MOV A, imm8', 2, 'mov', null, 'Load an immediate into A');
  define(0x11, 'MOV Rn, imm8', 2, 'mov', null, 'Load an immediate into Rn');
  define(0x12, 'MOV A, Rn', 1, 'mov', null, 'Copy Rn to A');
  define(0x13, 'MOV Rn, A', 1, 'mov', null, 'Copy A to Rn');
  define(0x14, 'MOV Rn, Rm', 1, 'mov', null, 'Copy Rm to Rn');
  define(0x15, 'MOV A, [addr]', 3, 'mov', null, 'Load A from memory (direct)');
  define(0x16, 'MOV [addr], A', 3, 'mov', null, 'Store A to memory (direct)');
  define(0x17, 'MOV A, [Rn]', 3, 'mov', null, 'Load A from the address held in Rn (register indirect)');
  define(0x18, 'MOV [Rn], A', 3, 'mov', null, 'Store A to the address held in Rn (register indirect)');
  define(0x19, 'MOV A, [addr+Rn]', 4, 'mov', null, 'Load A from addr + Rn (indexed)');
  define(0x1A, 'MOV [addr+Rn], A', 4, 'mov', null, 'Store A to addr + Rn (indexed)');
  define(0x1B, 'MOV Rn, [addr]', 3, 'mov', null, 'Load Rn from memory (direct)');
  define(0x1C, 'MOV [addr], Rn', 3, 'mov', null, 'Store Rn to memory (direct)');
  define(0x1D, 'MOV SP, imm8', 2, 'mov', null, 'Set the stack pointer');
  define(0x1E, 'XCHG A, Rn', 2, 'xchg', null, 'Swap A and Rn');

  // 0x2x - stack
  define(0x20, 'PUSH A', 3, 'push', null, 'Push A');
  define(0x21, 'PUSH Rn', 3, 'push', null, 'Push Rn');
  define(0x22, 'PUSH FLAGS', 3, 'push', null, 'Push the flags register');
  define(0x24, 'POP A', 3, 'pop', null, 'Pop into A');
  define(0x25, 'POP Rn', 3, 'pop', null, 'Pop into Rn');
  define(0x26, 'POP FLAGS', 3, 'pop', null, 'Pop into the flags register');

  // 0x3x - arithmetic with a second operand
  define(0x30, 'ADD A, imm8', 2, 'alu', { op: 'add' }, 'A = A + imm8');
  define(0x31, 'ADD A, Rn', 1, 'alu', { op: 'add' }, 'A = A + Rn');
  define(0x32, 'ADD A, [addr]', 3, 'alu', { op: 'add' }, 'A = A + [addr]');
  define(0x33, 'ADC A, imm8', 2, 'alu', { op: 'adc' }, 'A = A + imm8 + C');
  define(0x34, 'ADC A, Rn', 1, 'alu', { op: 'adc' }, 'A = A + Rn + C');
  define(0x35, 'ADC A, [addr]', 3, 'alu', { op: 'adc' }, 'A = A + [addr] + C');
  define(0x36, 'SUB A, imm8', 2, 'alu', { op: 'sub' }, 'A = A - imm8');
  define(0x37, 'SUB A, Rn', 1, 'alu', { op: 'sub' }, 'A = A - Rn');
  define(0x38, 'SUB A, [addr]', 3, 'alu', { op: 'sub' }, 'A = A - [addr]');
  define(0x39, 'SBB A, imm8', 2, 'alu', { op: 'sbb' }, 'A = A - imm8 - C');
  define(0x3A, 'SBB A, Rn', 1, 'alu', { op: 'sbb' }, 'A = A - Rn - C');
  define(0x3B, 'SBB A, [addr]', 3, 'alu', { op: 'sbb' }, 'A = A - [addr] - C');
  define(0x3C, 'CMP A, imm8', 2, 'alu', { op: 'cmp' }, 'Set flags from A - imm8');
  define(0x3D, 'CMP A, Rn', 1, 'alu', { op: 'cmp' }, 'Set flags from A - Rn');
  define(0x3E, 'CMP A, [addr]', 3, 'alu', { op: 'cmp' }, 'Set flags from A - [addr]');

  // 0x4x - single operand arithmetic
  define(0x40, 'INC A', 1, 'unary', { op: 'inc' }, 'A = A + 1 (C unchanged)');
  define(0x41, 'INC Rn', 1, 'unary', { op: 'inc' }, 'Rn = Rn + 1 (C unchanged)');
  define(0x42, 'INC [addr]', 4, 'unary', { op: 'inc' }, '[addr] = [addr] + 1 (read-modify-write)');
  define(0x43, 'DEC A', 1, 'unary', { op: 'dec' }, 'A = A - 1 (C unchanged)');
  define(0x44, 'DEC Rn', 1, 'unary', { op: 'dec' }, 'Rn = Rn - 1 (C unchanged)');
  define(0x45, 'DEC [addr]', 4, 'unary', { op: 'dec' }, '[addr] = [addr] - 1 (read-modify-write)');
  define(0x46, 'MUL Rn', 8, 'muldiv', { op: 'mul' }, 'A = low byte of A * Rn, C = V = overflow');
  define(0x47, 'DIV Rn', 12, 'muldiv', { op: 'div' }, 'A = A / Rn (unsigned), faults on zero');
  define(0x48, 'MOD Rn', 12, 'muldiv', { op: 'mod' }, 'A = A mod Rn (unsigned), faults on zero');
  define(0x49, 'NEG A', 1, 'unary', { op: 'neg' }, "A = -A (two's complement)");

  // 0x5x - logic and shifts
  define(0x50, 'AND A, imm8', 2, 'alu', { op: 'and' }, 'A = A and imm8');
  define(0x51, 'AND A, Rn', 1, 'alu', { op: 'and' }, 'A = A and Rn');
  define(0x52, 'OR A, imm8', 2, 'alu', { op: 'or' }, 'A = A or imm8');
  define(0x53, 'OR A, Rn', 1, 'alu', { op: 'or' }, 'A = A or Rn');
  define(0x54, 'XOR A, imm8', 2, 'alu', { op: 'xor' }, 'A = A xor imm8');
  define(0x55, 'XOR A, Rn', 1, 'alu', { op: 'xor' }, 'A = A xor Rn');
  define(0x56, 'NOT A', 1, 'unary', { op: 'not' }, 'A = not A');
  define(0x57, 'SHL A', 1, 'unary', { op: 'shl' }, 'Shift A left, C = old bit 7');
  define(0x58, 'SHR A', 1, 'unary', { op: 'shr' }, 'Shift A right, C = old bit 0');
  define(0x59, 'ROL A', 1, 'unary', { op: 'rol' }, 'Rotate A left, C = old bit 7');
  define(0x5A, 'ROR A', 1, 'unary', { op: 'ror' }, 'Rotate A right, C = old bit 0');

  // 0x6x - control flow
  define(0x60, 'JMP addr', 2, 'jump', { cond: 'always' }, 'Jump');
  define(0x61, 'JZ addr', 2, 'jump', { cond: 'z' }, 'Jump if Z = 1');
  define(0x62, 'JNZ addr', 2, 'jump', { cond: 'nz' }, 'Jump if Z = 0');
  define(0x63, 'JC addr', 2, 'jump', { cond: 'c' }, 'Jump if C = 1');
  define(0x64, 'JNC addr', 2, 'jump', { cond: 'nc' }, 'Jump if C = 0');
  define(0x65, 'JS addr', 2, 'jump', { cond: 's' }, 'Jump if N = 1');
  define(0x66, 'JNS addr', 2, 'jump', { cond: 'ns' }, 'Jump if N = 0');
  define(0x67, 'JO addr', 2, 'jump', { cond: 'o' }, 'Jump if V = 1');
  define(0x68, 'JNO addr', 2, 'jump', { cond: 'no' }, 'Jump if V = 0');
  define(0x69, 'CALL addr', 5, 'call', null, 'Push the return address and jump to a subroutine');
  define(0x6A, 'RET', 4, 'ret', null, 'Return from a subroutine');
  define(0x6B, 'LOOP Rn, addr', 3, 'loop', null, 'Decrement Rn and jump to addr while Rn is not zero');

  // 0x7x - flags
  define(0x70, 'CLC', 1, 'flag', { flag: 'c', value: false }, 'Clear the carry flag');
  define(0x71, 'STC', 1, 'flag', { flag: 'c', value: true }, 'Set the carry flag');

  // 0xFx - input / output
  define(0xF0, 'OUT port, A', 3, 'out', null, 'Write A to an output port (0 = decimal, 1 = character)');

  all.sort((x, y) => x.opcode - y.opcode);

  const byMnemonic = new Map();
  for (const i of all) {
    if (!byMnemonic.has(i.mnemonic)) byMnemonic.set(i.mnemonic, []);
    byMnemonic.get(i.mnemonic).push(i);
  }

  /** Decodes the instruction at `address` in a 256 byte memory array (mirror of Disassembler.cs). */
  function disassemble(memory, address) {
    address &= 0xFF;
    const opcode = memory[address];
    const instruction = byOpcode[opcode];
    if (!instruction) return { address, bytes: [opcode], text: '.db 0x' + hex2(opcode), instruction: null, length: 1 };
    const bytes = [opcode];
    for (let i = 0; i < instruction.syntax.operandBytes; i++) bytes.push(memory[(address + 1 + i) & 0xFF]);
    return { address, bytes, text: format(instruction.syntax, bytes.slice(1)), instruction, length: bytes.length };
  }

  global.CISC = global.CISC || {};
  global.CISC.ISA = {
    all, byOpcode, decode: (opcode) => byOpcode[opcode & 0xFF] || null,
    withMnemonic: (m) => byMnemonic.get(String(m).toUpperCase()) || [],
    mnemonics: () => Array.from(byMnemonic.keys()),
    isNumericKind: (kind) => NUMERIC_KINDS.has(kind),
    format, parseSyntax, disassemble, hex2,
  };
})(typeof window !== 'undefined' ? window : globalThis);
