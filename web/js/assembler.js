/*
 * CISC-16 assembler - JavaScript mirror of CISCProcessorLibrary/Assembler.cs.
 * Syntax:  [label:] MNEMONIC [operand[, operand]] [; comment]
 * Operands: A, R0..R7, SI, DI, BP, SP, FLAGS, number / label, [addr], [Rn], [addr+Rn], [SI], [BP+disp]
 * Numbers: 123, 0x7F, $7F, 0b1010, 'c'; expressions may add and subtract terms.
 * Directives: .org addr | .db v, "text" | .dw word, label | .ds count | .equ NAME, value
 */
(function (global) {
  'use strict';

  const ISA = global.CISC.ISA;
  const MEMORY_SIZE = 65536;
  const LABEL = /^\s*([A-Za-z_][A-Za-z0-9_]*)\s*:(.*)$/;
  const REGISTER = /^R([0-7])$/i;
  const XREGISTER = /^(SI|DI|BP|SP)$/i;
  const BASED = /^(SI|DI|BP|SP)\s*([+-].+)$/i;
  const IDENTIFIER = /^[A-Za-z_][A-Za-z0-9_]*$/;
  const RESERVED = new Set(['A', 'FLAGS', 'SI', 'DI', 'BP', 'SP', 'R0', 'R1', 'R2', 'R3', 'R4', 'R5', 'R6', 'R7']);

  class AssemblyError extends Error {
    constructor(line, detail) {
      super('Line ' + line + ': ' + detail);
      this.line = line;
      this.detail = detail;
    }
  }

  const xregCode = (name) => ISA.ADDRESS_REGISTERS.indexOf(name.toUpperCase());

  function stripComment(line) {
    let quote = null;
    for (let i = 0; i < line.length; i++) {
      const c = line[i];
      if (quote) { if (c === quote) quote = null; }
      else if (c === '"' || c === "'") quote = c;
      else if (c === ';') return line.slice(0, i);
    }
    return line;
  }

  function splitHead(text) {
    let i = 0;
    while (i < text.length && !/\s/.test(text[i])) i++;
    return [text.slice(0, i), text.slice(i).trim()];
  }

  function splitOperands(text) {
    const parts = [];
    let current = '';
    let depth = 0;
    let quote = null;
    for (const c of text) {
      if (quote) { current += c; if (c === quote) quote = null; continue; }
      if (c === '"' || c === "'") { quote = c; current += c; }
      else if (c === '[') { depth++; current += c; }
      else if (c === ']') { depth--; current += c; }
      else if (c === ',' && depth === 0) { parts.push(current.trim()); current = ''; }
      else current += c;
    }
    if (current.trim().length > 0 || parts.length > 0) parts.push(current.trim());
    return parts.filter((p) => p.length > 0 || parts.length > 1);
  }

  function parseNumber(text) {
    if (/^0x[0-9a-f]+$/i.test(text)) return parseInt(text.slice(2), 16);
    if (/^\$[0-9a-f]+$/i.test(text)) return parseInt(text.slice(1), 16);
    if (/^0b[01]+$/i.test(text)) return parseInt(text.slice(2), 2);
    if (/^[0-9]+$/.test(text)) return parseInt(text, 10);
    return null;
  }

  function evaluate(expression, symbols, line) {
    expression = expression.trim();
    if (expression.length === 0) throw new AssemblyError(line, 'Empty expression.');
    let value = 0, sign = 1, i = 0, expectTerm = true;
    while (i < expression.length) {
      const c = expression[i];
      if (/\s/.test(c)) { i++; continue; }
      if (expectTerm) {
        if (c === '-') { sign = -sign; i++; continue; }
        if (c === '+') { i++; continue; }
        const start = i;
        if (c === "'") {
          const close = expression.indexOf("'", i + 1);
          if (close !== i + 2) throw new AssemblyError(line, "Malformed character literal in '" + expression + "'.");
          value += sign * expression.charCodeAt(i + 1);
          i = close + 1;
        } else {
          while (i < expression.length && /[A-Za-z0-9_$]/.test(expression[i])) i++;
          if (i === start) throw new AssemblyError(line, "Unexpected character '" + c + "' in expression '" + expression + "'.");
          const term = expression.slice(start, i);
          const number = parseNumber(term);
          if (number !== null) value += sign * number;
          else if (IDENTIFIER.test(term)) {
            if (!symbols.has(term.toUpperCase())) throw new AssemblyError(line, "Unknown symbol '" + term + "'.");
            value += sign * symbols.get(term.toUpperCase());
          } else throw new AssemblyError(line, "Invalid number '" + term + "'.");
        }
        sign = 1;
        expectTerm = false;
      } else {
        if (c === '+') sign = 1;
        else if (c === '-') sign = -1;
        else throw new AssemblyError(line, "Unexpected character '" + c + "' in expression '" + expression + "'.");
        i++;
        expectTerm = true;
      }
    }
    if (expectTerm) throw new AssemblyError(line, "Expression '" + expression + "' ends with an operator.");
    return value;
  }

  function toByte(value, line, what) {
    if (value < -128 || value > 255) throw new AssemblyError(line, what + ' ' + value + ' does not fit in one byte (-128..255).');
    return value & 0xFF;
  }
  function toDisplacement(value, line) {
    if (value < -128 || value > 127) throw new AssemblyError(line, 'Displacement ' + value + ' does not fit in a signed byte (-128..127).');
    return value & 0xFF;
  }
  function toWord(value, line, what) {
    if (value < -32768 || value > 65535) throw new AssemblyError(line, what + ' ' + value + ' does not fit in 16 bits (-32768..65535).');
    return value & 0xFFFF;
  }

  function parseOperand(text, line) {
    text = text.trim();
    if (text.length === 0) throw new AssemblyError(line, 'Empty operand.');
    const upper = text.toUpperCase();
    if (upper === 'A') return { kind: 'acc' };
    if (upper === 'FLAGS') return { kind: 'flags' };
    let m = REGISTER.exec(text);
    if (m) return { kind: 'reg', register: Number(m[1]) };
    m = XREGISTER.exec(text);
    if (m) return { kind: 'xreg', register: xregCode(m[1]) };
    if (text.startsWith('[') && text.endsWith(']')) {
      const inner = text.slice(1, -1).trim();
      if (inner.length === 0) throw new AssemblyError(line, 'Empty memory operand [].');
      m = REGISTER.exec(inner);
      if (m) return { kind: 'indirect', register: Number(m[1]) };
      m = XREGISTER.exec(inner);
      if (m) return { kind: 'xind', register: xregCode(m[1]) };
      m = BASED.exec(inner);
      if (m) return { kind: 'xdisp', register: xregCode(m[1]), expression: m[2] };
      const plus = inner.lastIndexOf('+');
      if (plus > 0) {
        const left = inner.slice(0, plus).trim();
        const right = inner.slice(plus + 1).trim();
        const rightReg = REGISTER.exec(right);
        const leftReg = REGISTER.exec(left);
        if (rightReg) return { kind: 'indexed', expression: left, register: Number(rightReg[1]) };
        if (leftReg) return { kind: 'indexed', expression: right, register: Number(leftReg[1]) };
      }
      return { kind: 'direct', expression: inner };
    }
    if (text.includes('[') || text.includes(']')) throw new AssemblyError(line, "Malformed memory operand '" + text + "'.");
    return { kind: 'imm', expression: text };
  }

  function matches(syntax, operands) {
    if (syntax.operands.length !== operands.length) return false;
    return syntax.operands.every((slot, i) => {
      const operand = operands[i];
      if (slot.kind === 'literal') return operand.kind === 'imm' && String(operand.expression).toUpperCase() === slot.literal;
      return slot.kind === operand.kind || (ISA.isNumericKind(slot.kind) && ISA.isNumericKind(operand.kind));
    });
  }

  function isString(item) { return item.length >= 2 && item[0] === '"' && item[item.length - 1] === '"'; }

  function defineSymbol(symbols, name, value, line) {
    const key = name.toUpperCase();
    if (RESERVED.has(key)) throw new AssemblyError(line, "'" + name + "' is a register name and cannot be used as a label.");
    if (symbols.has(key)) throw new AssemblyError(line, "Symbol '" + name + "' is defined more than once.");
    symbols.set(key, value);
  }

  function assemble(source) {
    const symbols = new Map();
    const statements = [];
    const lines = String(source).replace(/\r\n/g, '\n').split('\n');
    let location = 0;
    let entryPoint = null;

    // Pass 1: parse, pick the instruction form, assign addresses, collect labels.
    lines.forEach((rawLine, index) => {
      const lineNumber = index + 1;
      const statement = { lineNumber, source: rawLine, address: location, length: 0, instruction: null, operands: [], dataItems: null, dataWords: false, reserved: 0 };
      statements.push(statement);

      let text = stripComment(rawLine).trim();
      if (text.length === 0) return;

      const label = LABEL.exec(text);
      if (label) {
        defineSymbol(symbols, label[1], location, lineNumber);
        text = label[2].trim();
        if (text.length === 0) return;
      }

      if (text.startsWith('.')) {
        const [name, args] = splitHead(text);
        switch (name.toUpperCase()) {
          case '.ORG': {
            if (!args) throw new AssemblyError(lineNumber, '.org needs a value.');
            const address = evaluate(args, symbols, lineNumber);
            if (address < 0 || address >= MEMORY_SIZE) throw new AssemblyError(lineNumber, '.org address ' + address + ' is outside memory (0..' + (MEMORY_SIZE - 1) + ').');
            location = address;
            statement.address = address;
            break;
          }
          case '.EQU': {
            const parts = splitOperands(args);
            if (parts.length !== 2 || !IDENTIFIER.test(parts[0])) throw new AssemblyError(lineNumber, '.equ expects a name and a value: .equ NAME, value');
            if (!parts[1]) throw new AssemblyError(lineNumber, '.equ needs a value.');
            defineSymbol(symbols, parts[0], evaluate(parts[1], symbols, lineNumber), lineNumber);
            break;
          }
          case '.DB':
          case '.DW': {
            const words = name.toUpperCase() === '.DW';
            const items = splitOperands(args);
            if (items.length === 0) throw new AssemblyError(lineNumber, name + ' needs at least one value.');
            statement.dataItems = items;
            statement.dataWords = words;
            statement.length = items.reduce((n, item) => n + (isString(item) ? item.length - 2 : (words ? 2 : 1)), 0);
            if (location + statement.length > MEMORY_SIZE) throw new AssemblyError(lineNumber, 'Program does not fit in ' + MEMORY_SIZE + ' bytes of memory.');
            statement.address = location;
            location += statement.length;
            break;
          }
          case '.DS': {
            if (!args) throw new AssemblyError(lineNumber, '.ds needs a value.');
            const count = evaluate(args, symbols, lineNumber);
            if (count < 0 || location + count > MEMORY_SIZE) throw new AssemblyError(lineNumber, '.ds ' + count + ' does not fit in memory.');
            statement.reserved = count;
            statement.length = count;
            statement.address = location;
            location += count;
            break;
          }
          default:
            throw new AssemblyError(lineNumber, "Unknown directive '" + name + "'. Supported: .org, .db, .dw, .ds, .equ");
        }
        return;
      }

      const [mnemonic, operandText] = splitHead(text);
      const operands = splitOperands(operandText).map((o) => parseOperand(o, lineNumber));
      const forms = ISA.withMnemonic(mnemonic);
      if (forms.length === 0) throw new AssemblyError(lineNumber, "Unknown mnemonic '" + mnemonic.toUpperCase() + "'.");
      const match = forms.find((f) => matches(f.syntax, operands));
      if (!match) throw new AssemblyError(lineNumber, 'Invalid operands for ' + mnemonic.toUpperCase() + '. Valid forms: ' + forms.map((f) => f.syntax.template).join(' | '));

      statement.instruction = match;
      statement.operands = operands;
      statement.length = match.length;
      if (entryPoint === null) entryPoint = location;
      if (location + statement.length > MEMORY_SIZE) throw new AssemblyError(lineNumber, 'Program does not fit in ' + MEMORY_SIZE + ' bytes of memory.');
      statement.address = location;
      location += statement.length;
    });

    // Pass 2: resolve expressions and emit bytes.
    const occupied = new Uint8Array(MEMORY_SIZE);
    const listing = statements.map((st) => {
      const bytes = emit(st, symbols);
      for (let i = 0; i < bytes.length; i++) {
        const address = st.address + i;
        if (address >= MEMORY_SIZE) throw new AssemblyError(st.lineNumber, 'Program does not fit in ' + MEMORY_SIZE + ' bytes of memory.');
        if (occupied[address]) throw new AssemblyError(st.lineNumber, 'Address 0x' + ISA.hex4(address) + ' is emitted twice (check your .org directives).');
        occupied[address] = 1;
      }
      return { lineNumber: st.lineNumber, address: st.address, bytes, source: st.source, isInstruction: st.instruction !== null, isData: st.instruction === null && bytes.length > 0 };
    });

    const symbolTable = {};
    for (const [k, v] of symbols) symbolTable[k] = v;
    return { listing, symbols: symbolTable, entryPoint: entryPoint === null ? 0 : entryPoint, length: listing.reduce((n, l) => n + l.bytes.length, 0) };
  }

  function addWord(bytes, value) { bytes.push(value & 0xFF, (value >> 8) & 0xFF); }

  function emit(st, symbols) {
    const line = st.lineNumber;
    if (st.instruction) {
      const bytes = [st.instruction.opcode];
      st.instruction.syntax.operands.forEach((slot, i) => {
        const operand = st.operands[i];
        switch (slot.kind) {
          case 'reg': case 'indirect': case 'xreg': case 'xind':
            bytes.push(operand.register);
            break;
          case 'imm':
            bytes.push(toByte(evaluate(operand.expression, symbols, line), line, 'Value'));
            break;
          case 'imm16': case 'addr': case 'direct':
            addWord(bytes, toWord(evaluate(operand.expression, symbols, line), line, slot.kind === 'imm16' ? 'Value' : 'Address'));
            break;
          case 'indexed':
            addWord(bytes, toWord(evaluate(operand.expression, symbols, line), line, 'Address'));
            bytes.push(operand.register);
            break;
          case 'xdisp':
            bytes.push(operand.register);
            bytes.push(toDisplacement(evaluate(operand.expression, symbols, line), line));
            break;
          default:
            break;
        }
      });
      return bytes;
    }
    if (st.dataItems) {
      const bytes = [];
      for (const item of st.dataItems) {
        if (isString(item)) {
          for (const ch of item.slice(1, -1)) {
            const code = ch.charCodeAt(0);
            if (code > 0xFF) throw new AssemblyError(line, "Character '" + ch + "' is not an 8 bit character.");
            bytes.push(code);
          }
        } else if (st.dataWords) {
          addWord(bytes, toWord(evaluate(item, symbols, line), line, 'Value'));
        } else {
          bytes.push(toByte(evaluate(item, symbols, line), line, 'Value'));
        }
      }
      return bytes;
    }
    if (st.reserved > 0) return new Array(st.reserved).fill(0);
    return [];
  }

  global.CISC.Assembler = { assemble, AssemblyError };
})(typeof window !== 'undefined' ? window : globalThis);
