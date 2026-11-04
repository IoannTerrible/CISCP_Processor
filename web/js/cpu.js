/*
 * CISC-8 processor - JavaScript mirror of CISCProcessorLibrary/CISCProcessor.cs and ArithmeticLogicUnit.cs.
 * step() runs one fetch - decode - execute cycle and returns a trace with the same micro-operations
 * the .NET implementation reports, which is what the visualiser animates.
 */
(function (global) {
  'use strict';

  const ISA = global.CISC.ISA;
  const FLAG = { z: 1 << 0, c: 1 << 1, n: 1 << 2, v: 1 << 3 };
  const MEMORY_SIZE = 256;
  const INITIAL_SP = 0xFF;

  class ProcessorFault extends Error {
    constructor(kind, message) {
      super(message);
      this.kind = kind;
    }
  }

  class CPU {
    constructor() {
      this.memory = new Uint8Array(MEMORY_SIZE);
      this.r = new Uint8Array(8);
      this.onOutput = null;       // (port, value) => void
      this.onMicroOp = null;      // (op) => void
      this.reset(true);
    }

    reset(clearMemory) {
      this.a = 0;
      this.r.fill(0);
      this.ip = 0;
      this.sp = INITIAL_SP;
      this.flags = 0;
      this.ir = 0;
      this.mar = 0;
      this.mdr = 0;
      this.phase = 'idle';
      this.cycles = 0;
      this.instructions = 0;
      this.halted = false;
      this.fault = null;
      this.faultMessage = null;
      this.lastStep = null;
      this._ops = null;
      this._extraCycles = 0;
      if (clearMemory) this.memory.fill(0);
    }

    /** Loads an assembled program (from CISC.Assembler) and points IP at its entry point. */
    loadProgram(program) {
      this.reset(true);
      for (const line of program.listing) {
        for (let i = 0; i < line.bytes.length; i++) this.memory[(line.address + i) & 0xFF] = line.bytes[i];
      }
      this.ip = program.entryPoint & 0xFF;
    }

    loadBytes(bytes, origin) {
      this.reset(true);
      origin = origin || 0;
      for (let i = 0; i < bytes.length; i++) this.memory[(origin + i) & 0xFF] = bytes[i];
      this.ip = origin & 0xFF;
    }

    flag(name) { return (this.flags & FLAG[name]) !== 0; }

    setFlag(name, value) {
      this.setFlags(value ? (this.flags | FLAG[name]) : (this.flags & ~FLAG[name]));
    }

    setFlags(value) {
      value &= 0x0F;
      if (value === this.flags) return;
      const before = this.flags;
      this.flags = value;
      this.record({ kind: 'flags', name: 'FLAGS', before, after: value, value, text: CPU.formatFlags(value) });
    }

    static formatFlags(flags) {
      return (flags & FLAG.z ? 'Z' : '-') + (flags & FLAG.c ? 'C' : '-') + (flags & FLAG.n ? 'N' : '-') + (flags & FLAG.v ? 'V' : '-');
    }

    getState() {
      return {
        a: this.a, r: Array.from(this.r), ip: this.ip, sp: this.sp, flags: this.flags, ir: this.ir,
        mar: this.mar, mdr: this.mdr, phase: this.phase, cycles: this.cycles, instructions: this.instructions,
        halted: this.halted, fault: this.fault, faultMessage: this.faultMessage, flagsText: CPU.formatFlags(this.flags),
      };
    }

    record(op) {
      if (this._ops) this._ops.push(op);
      if (this.onMicroOp) this.onMicroOp(op);
    }

    _change(name, field, value) {
      value &= 0xFF;
      if (this[field] === value) return;
      const before = this[field];
      this[field] = value;
      this.record({ kind: 'register', name, before, after: value, value });
    }

    setA(v) { this._change('A', 'a', v); }
    setIP(v) { this._change('IP', 'ip', v); }
    setSP(v) { this._change('SP', 'sp', v); }
    setIR(v) { this._change('IR', 'ir', v); }
    setMAR(v) { this._change('MAR', 'mar', v); }
    setMDR(v) { this._change('MDR', 'mdr', v); }

    readRegister(index) { return this.r[index & 7]; }

    writeRegister(index, value) {
      index &= 7;
      value &= 0xFF;
      if (this.r[index] === value) return;
      const before = this.r[index];
      this.r[index] = value;
      this.record({ kind: 'register', name: 'R' + index, before, after: value, value });
    }

    readMemory(address, purpose) {
      address &= 0xFF;
      this.setMAR(address);
      const value = this.memory[address];
      if (purpose === 'opcode') this.record({ kind: 'fetch', address, value, name: 'opcode' });
      else this.record({ kind: 'read', address, value, name: purpose });
      this.setMDR(value);
      return value;
    }

    writeMemory(address, value, purpose) {
      address &= 0xFF;
      value &= 0xFF;
      this.setMAR(address);
      this.setMDR(value);
      this.memory[address] = value;
      this.record({ kind: 'write', address, value, name: purpose });
    }

    push(value) {
      if (this.sp === 0) this.raiseFault('StackOverflow', 'Stack overflow: SP is already 0x00');
      this.writeMemory(this.sp, value, 'stack');
      this.setSP(this.sp - 1);
    }

    pop() {
      if (this.sp === INITIAL_SP) this.raiseFault('StackUnderflow', 'Stack underflow: the stack is empty');
      this.setSP(this.sp + 1);
      return this.readMemory(this.sp, 'stack');
    }

    jump(condition, target, taken) {
      this.record({ kind: 'jump', name: condition, address: target, value: taken ? 1 : 0 });
      if (taken) {
        this.setIP(target);
        this._extraCycles += 1;
      }
    }

    output(port, value) {
      this.record({ kind: 'output', address: port, value, name: 'port' });
      if (this.onOutput) this.onOutput(port, value);
    }

    halt() {
      this.halted = true;
      this.record({ kind: 'halt', name: 'HLT' });
    }

    raiseFault(kind, message) {
      throw new ProcessorFault(kind, message);
    }

    // ---- ALU (mirror of ArithmeticLogicUnit.cs) ----------------------------------------------

    _setAluFlags(result, carry, overflow) {
      let flags = 0;
      if (result === 0) flags |= FLAG.z;
      if (carry) flags |= FLAG.c;
      if (result & 0x80) flags |= FLAG.n;
      if (overflow) flags |= FLAG.v;
      this.setFlags(flags);
    }

    _alu(name, a, b, result) {
      this.record({ kind: 'alu', name, before: a, after: b, value: result });
      return result;
    }

    aluAdd(a, b, useCarry) {
      const carryIn = useCarry && this.flag('c') ? 1 : 0;
      const full = a + b + carryIn;
      const result = full & 0xFF;
      this._setAluFlags(result, full > 0xFF, ((a ^ result) & (b ^ result) & 0x80) !== 0);
      return this._alu(useCarry ? 'ADC' : 'ADD', a, b, result);
    }

    aluSub(a, b, useBorrow, name) {
      const borrowIn = useBorrow && this.flag('c') ? 1 : 0;
      const full = a - b - borrowIn;
      const result = full & 0xFF;
      this._setAluFlags(result, full < 0, ((a ^ b) & (a ^ result) & 0x80) !== 0);
      return this._alu(name, a, b, result);
    }

    aluLogic(name, a, b, result) {
      this._setAluFlags(result, false, false);
      return this._alu(name, a, b, result);
    }

    aluShift(name, a, result, carry) {
      this._setAluFlags(result, carry, false);
      return this._alu(name, a, 1, result);
    }

    aluBinary(op, a, b) {
      switch (op) {
        case 'add': return this.aluAdd(a, b, false);
        case 'adc': return this.aluAdd(a, b, true);
        case 'sub': return this.aluSub(a, b, false, 'SUB');
        case 'sbb': return this.aluSub(a, b, true, 'SBB');
        case 'cmp': return this.aluSub(a, b, false, 'CMP');
        case 'and': return this.aluLogic('AND', a, b, a & b);
        case 'or': return this.aluLogic('OR', a, b, a | b);
        case 'xor': return this.aluLogic('XOR', a, b, a ^ b);
        default: throw new Error('Unknown ALU operation ' + op);
      }
    }

    aluUnary(op, a) {
      switch (op) {
        case 'inc': {
          const result = (a + 1) & 0xFF;
          this._setAluFlags(result, this.flag('c'), a === 0x7F);
          return this._alu('INC', a, 1, result);
        }
        case 'dec': {
          const result = (a - 1) & 0xFF;
          this._setAluFlags(result, this.flag('c'), a === 0x80);
          return this._alu('DEC', a, 1, result);
        }
        case 'not': return this.aluLogic('NOT', a, 0, (~a) & 0xFF);
        case 'neg': {
          const result = (-a) & 0xFF;
          this._setAluFlags(result, a !== 0, a === 0x80);
          return this._alu('NEG', a, 0, result);
        }
        case 'shl': return this.aluShift('SHL', a, (a << 1) & 0xFF, (a & 0x80) !== 0);
        case 'shr': return this.aluShift('SHR', a, a >> 1, (a & 0x01) !== 0);
        case 'rol': return this.aluShift('ROL', a, ((a << 1) | (a >> 7)) & 0xFF, (a & 0x80) !== 0);
        case 'ror': return this.aluShift('ROR', a, ((a >> 1) | (a << 7)) & 0xFF, (a & 0x01) !== 0);
        default: throw new Error('Unknown unary operation ' + op);
      }
    }

    aluMulDiv(op, a, b) {
      switch (op) {
        case 'mul': {
          const product = a * b;
          const result = product & 0xFF;
          this._setAluFlags(result, product > 0xFF, product > 0xFF);
          return this._alu('MUL', a, b, result);
        }
        case 'div': {
          if (b === 0) this.raiseFault('DivideByZero', 'DIV by zero');
          const result = Math.floor(a / b);
          this._setAluFlags(result, false, false);
          return this._alu('DIV', a, b, result);
        }
        case 'mod': {
          if (b === 0) this.raiseFault('DivideByZero', 'MOD by zero');
          const result = a % b;
          this._setAluFlags(result, false, false);
          return this._alu('MOD', a, b, result);
        }
        default: throw new Error('Unknown operation ' + op);
      }
    }

    // ---- addressing modes (mirror of Instruction.cs) -----------------------------------------

    readOperand(slot, ops) {
      switch (slot.kind) {
        case 'acc': return this.a;
        case 'reg': return this.readRegister(ops[slot.offset]);
        case 'imm': return ops[slot.offset];
        case 'addr': return ops[slot.offset];
        case 'direct': return this.readMemory(ops[slot.offset], 'data');
        case 'indirect': return this.readMemory(this.readRegister(ops[slot.offset]), 'data');
        case 'indexed': return this.readMemory(ops[slot.offset] + this.readRegister(ops[slot.offset + 1]), 'data');
        case 'sp': return this.sp;
        case 'flags': return this.flags;
        default: throw new Error('Unsupported operand kind ' + slot.kind);
      }
    }

    writeOperand(slot, ops, value) {
      switch (slot.kind) {
        case 'acc': this.setA(value); break;
        case 'reg': this.writeRegister(ops[slot.offset], value); break;
        case 'direct': this.writeMemory(ops[slot.offset], value, 'data'); break;
        case 'indirect': this.writeMemory(this.readRegister(ops[slot.offset]), value, 'data'); break;
        case 'indexed': this.writeMemory(ops[slot.offset] + this.readRegister(ops[slot.offset + 1]), value, 'data'); break;
        case 'sp': this.setSP(value); break;
        case 'flags': this.setFlags(value); break;
        default: throw new Error('Operand kind ' + slot.kind + ' cannot be written');
      }
    }

    // ---- instruction semantics (mirror of the Instructions/ folder) ---------------------------

    execute(instruction, ops) {
      const slots = instruction.syntax.operands;
      switch (instruction.family) {
        case 'nop':
          break;
        case 'hlt':
          this.halt();
          break;
        case 'mov':
          this.writeOperand(slots[0], ops, this.readOperand(slots[1], ops));
          break;
        case 'xchg': {
          const a = this.a;
          const r = this.readOperand(slots[1], ops);
          this.setA(r);
          this.writeOperand(slots[1], ops, a);
          break;
        }
        case 'push':
          this.push(this.readOperand(slots[0], ops));
          break;
        case 'pop':
          this.writeOperand(slots[0], ops, this.pop());
          break;
        case 'alu': {
          const result = this.aluBinary(instruction.op, this.a, this.readOperand(slots[1], ops));
          if (instruction.op !== 'cmp') this.setA(result);
          break;
        }
        case 'unary': {
          const value = this.readOperand(slots[0], ops);
          this.writeOperand(slots[0], ops, this.aluUnary(instruction.op, value));
          break;
        }
        case 'muldiv':
          this.setA(this.aluMulDiv(instruction.op, this.a, this.readOperand(slots[0], ops)));
          break;
        case 'jump':
          this.jump(instruction.mnemonic, this.readOperand(slots[0], ops), this.conditionHolds(instruction.cond));
          break;
        case 'call': {
          const target = this.readOperand(slots[0], ops);
          this.push(this.ip);
          this.jump('CALL', target, true);
          break;
        }
        case 'ret':
          this.jump('RET', this.pop(), true);
          break;
        case 'loop': {
          const counter = (this.readOperand(slots[0], ops) - 1) & 0xFF;
          this.writeOperand(slots[0], ops, counter);
          this.jump('LOOP', this.readOperand(slots[1], ops), counter !== 0);
          break;
        }
        case 'flag':
          this.setFlag(instruction.flag, instruction.value);
          break;
        case 'out':
          this.output(this.readOperand(slots[0], ops), this.a);
          break;
        default:
          throw new Error('Unknown instruction family ' + instruction.family);
      }
    }

    conditionHolds(cond) {
      switch (cond) {
        case 'always': return true;
        case 'z': return this.flag('z');
        case 'nz': return !this.flag('z');
        case 'c': return this.flag('c');
        case 'nc': return !this.flag('c');
        case 's': return this.flag('n');
        case 'ns': return !this.flag('n');
        case 'o': return this.flag('v');
        case 'no': return !this.flag('v');
        default: return false;
      }
    }

    // ---- control unit ------------------------------------------------------------------------

    /** Executes one instruction and returns its trace. Throws if the processor is halted. */
    step() {
      if (this.halted) throw new Error('The processor is halted. Reset it or load a new program before stepping.');

      const ops = [];
      this._ops = ops;
      this._extraCycles = 0;
      const address = this.ip;
      let opcode = 0;
      let operandBytes = [];
      let disassembly = '?';
      let cycles = 0;

      try {
        this.phase = 'fetch';
        opcode = this.readMemory(this.ip, 'opcode');
        this.setIR(opcode);
        this.setIP(this.ip + 1);

        this.phase = 'decode';
        const instruction = ISA.decode(opcode);
        if (!instruction) this.raiseFault('InvalidOpcode', 'Invalid opcode 0x' + ISA.hex2(opcode) + ' at address 0x' + ISA.hex2(address));

        for (let i = 0; i < instruction.syntax.operandBytes; i++) {
          operandBytes.push(this.readMemory(this.ip, 'operand'));
          this.setIP(this.ip + 1);
        }
        disassembly = ISA.format(instruction.syntax, operandBytes);
        this.record({ kind: 'decode', value: opcode, name: instruction.mnemonic, text: disassembly });

        this.phase = 'execute';
        this.record({ kind: 'execute', name: instruction.mnemonic });
        this.execute(instruction, operandBytes);

        cycles = instruction.cycles + this._extraCycles;
        this.cycles += cycles;
        this.instructions += 1;
        if (this.halted) this.phase = 'halted';
      } catch (error) {
        if (!(error instanceof ProcessorFault)) { this._ops = null; throw error; }
        this.halted = true;
        this.fault = error.kind;
        this.faultMessage = error.message;
        this.phase = 'halted';
        this.record({ kind: 'fault', name: error.kind, text: error.message });
      } finally {
        this._ops = null;
      }

      const trace = { address, bytes: [opcode].concat(operandBytes), disassembly, cycles, ops, state: this.getState() };
      this.lastStep = trace;
      return trace;
    }

    /** Runs until HLT, a fault or the instruction budget is used up. */
    run(maxInstructions) {
      maxInstructions = maxInstructions || 100000;
      let executed = 0;
      while (!this.halted && executed < maxInstructions) {
        this.step();
        executed++;
      }
      return {
        reason: this.fault ? 'fault' : (this.halted ? 'halted' : 'limit'),
        instructions: this.instructions, cycles: this.cycles, fault: this.fault, faultMessage: this.faultMessage,
        state: this.getState(),
      };
    }
  }

  CPU.FLAG = FLAG;
  CPU.MEMORY_SIZE = MEMORY_SIZE;
  CPU.INITIAL_SP = INITIAL_SP;
  CPU.ProcessorFault = ProcessorFault;

  global.CISC.CPU = CPU;
})(typeof window !== 'undefined' ? window : globalThis);
