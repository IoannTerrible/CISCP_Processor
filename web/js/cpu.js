/*
 * CISC-16 processor - JavaScript mirror of CISCProcessorLibrary/CISCProcessor.cs, ArithmeticLogicUnit.cs
 * and the Devices folder. step() performs one fetch - decode - execute cycle (or an interrupt
 * acknowledge, or an idle quantum while waiting) and returns a trace with the same micro-operations
 * the .NET implementation reports, which is what the visualiser animates.
 */
(function (global) {
  'use strict';

  const ISA = global.CISC.ISA;
  const Timing = global.CISC.Timing;
  const FLAG = { z: 1 << 0, c: 1 << 1, n: 1 << 2, v: 1 << 3, i: 1 << 4 };
  const MEMORY_SIZE = 65536;
  const INITIAL_SP = 0xFFF0;
  const VECTOR_TABLE = 0xFFF0;
  const XREG = ['SI', 'DI', 'BP', 'SP'];
  const SP = 3;
  const COUNT_REGISTER = 6;

  class ProcessorFault extends Error {
    constructor(kind, message) { super(message); this.kind = kind; }
  }

  /** Programmable interval timer on IRQ 0 (ports 2, 3 = period, 4 = control, 5 = ticks). */
  class TimerDevice {
    constructor(cpu) {
      this.cpu = cpu;
      this.name = 'Programmable interval timer';
      this.reset();
      cpu.mapOutput(2, (v) => { this.period = (this.period & 0xFF00) | v; });
      cpu.mapOutput(3, (v) => { this.period = (this.period & 0x00FF) | (v << 8); });
      cpu.mapOutput(4, (v) => { this.enabled = (v & 1) !== 0; if (!this.enabled) this.counter = 0; });
      cpu.mapInput(2, () => this.period & 0xFF);
      cpu.mapInput(3, () => this.period >> 8);
      cpu.mapInput(4, () => (this.enabled ? 1 : 0));
      cpu.mapInput(5, () => this.ticks & 0xFF);
    }
    reset() { this.period = 0; this.enabled = false; this.counter = 0; this.ticks = 0; }
    tick(cycles) {
      if (!this.enabled || this.period === 0) return;
      this.counter += cycles;
      while (this.counter >= this.period) { this.counter -= this.period; this.ticks++; this.cpu.requestInterrupt(0); }
    }
  }

  /** Input device behind ports 0, 1 (next byte) and 6 (status). */
  class InputDevice {
    constructor(cpu) {
      this.name = 'Input device';
      this.queue = [];
      this.onEmpty = null;
      cpu.mapInput(0, () => this.readByte());
      cpu.mapInput(1, () => this.readByte());
      cpu.mapInput(6, () => (this.queue.length > 0 ? 1 : 0));
    }
    get available() { return this.queue.length; }
    enqueue(text) { for (const ch of String(text)) this.queue.push(ch.charCodeAt(0) & 0xFF); }
    clear() { this.queue = []; }
    peekText() { return String.fromCharCode.apply(null, this.queue); }
    reset() { this.queue = []; }
    tick() {}
    readByte() {
      if (this.queue.length === 0 && this.onEmpty) { const more = this.onEmpty(); if (more) this.enqueue(more); }
      return this.queue.length > 0 ? this.queue.shift() : 0;
    }
  }

  class CPU {
    constructor(microarchitecture) {
      this.memory = new Uint8Array(MEMORY_SIZE);
      this.r = new Uint8Array(8);
      this.x = new Uint16Array(4);
      this.readers = new Array(256).fill(null);
      this.writers = new Array(256).fill(null);
      this.onOutput = null;       // (port, value) => void
      this.onMicroOp = null;      // (op) => void
      this.timing = Timing.create(microarchitecture || 'Classic1975', this);
      this.timer = new TimerDevice(this);
      this.input = new InputDevice(this);
      this.devices = [this.timer, this.input];
      this.reset(true);
    }

    get microarchitecture() { return this.timing.kind; }
    set microarchitecture(kind) { if (kind !== this.timing.kind) this.timing = Timing.create(kind, this); }

    mapInput(port, read) { this.readers[port & 0xFF] = read; }
    mapOutput(port, write) { this.writers[port & 0xFF] = write; }

    reset(clearMemory) {
      this.a = 0;
      this.r.fill(0);
      this.x.fill(0);
      this.x[SP] = INITIAL_SP;
      this.stackBase = INITIAL_SP;
      this.ip = 0;
      this.flags = 0;
      this.ir = 0;
      this.mar = 0;
      this.mdr = 0;
      this.pending = 0;
      this.phase = 'idle';
      this.cycles = 0;
      this.instructions = 0;
      this.halted = false;
      this.waiting = false;
      this.fault = null;
      this.faultMessage = null;
      this.lastStep = null;
      this._ops = null;
      this._stepCycles = 0;
      this._stepAddress = 0;
      this.timing.reset();
      for (const d of this.devices) d.reset();
      if (clearMemory) this.memory.fill(0);
    }

    loadProgram(program) {
      this.reset(true);
      for (const line of program.listing) {
        for (let i = 0; i < line.bytes.length; i++) this.memory[(line.address + i) & 0xFFFF] = line.bytes[i];
      }
      this.ip = program.entryPoint & 0xFFFF;
    }

    loadBytes(bytes, origin) {
      this.reset(true);
      origin = origin || 0;
      for (let i = 0; i < bytes.length; i++) this.memory[(origin + i) & 0xFFFF] = bytes[i];
      this.ip = origin & 0xFFFF;
    }

    requestInterrupt(irq) { this.pending |= 1 << (irq & 7); }

    flag(name) { return (this.flags & FLAG[name]) !== 0; }
    setFlag(name, value) { this.setFlags(value ? (this.flags | FLAG[name]) : (this.flags & ~FLAG[name])); }
    setFlags(value) {
      value &= 0x1F;
      if (value === this.flags) return;
      const before = this.flags;
      this.flags = value;
      this.record({ kind: 'flags', name: 'FLAGS', before, after: value, value, text: CPU.formatFlags(value) });
    }
    static formatFlags(flags) {
      return (flags & FLAG.z ? 'Z' : '-') + (flags & FLAG.c ? 'C' : '-') + (flags & FLAG.n ? 'N' : '-') + (flags & FLAG.v ? 'V' : '-') + (flags & FLAG.i ? 'I' : '-');
    }

    getState() {
      return {
        a: this.a, r: Array.from(this.r), ip: this.ip, sp: this.x[SP], bp: this.x[2], si: this.x[0], di: this.x[1],
        flags: this.flags, ir: this.ir, mar: this.mar, mdr: this.mdr, pending: this.pending, phase: this.phase,
        cycles: this.cycles, instructions: this.instructions, halted: this.halted, waiting: this.waiting,
        fault: this.fault, faultMessage: this.faultMessage, flagsText: CPU.formatFlags(this.flags),
        timerPeriod: this.timer.period, timerEnabled: this.timer.enabled, timerTicks: this.timer.ticks, inputQueued: this.input.available,
      };
    }

    record(op) {
      if (this._ops) this._ops.push(op);
      if (this.onMicroOp) this.onMicroOp(op);
    }

    addCycles(n) { this._stepCycles += n; }

    _change(name, field, value, mask) {
      value &= mask;
      if (this[field] === value) return;
      const before = this[field];
      this[field] = value;
      this.record({ kind: 'register', name, before, after: value, value });
    }
    setA(v) { this._change('A', 'a', v, 0xFF); }
    setIP(v) { this._change('IP', 'ip', v, 0xFFFF); }
    setIR(v) { this._change('IR', 'ir', v, 0xFF); }
    setMAR(v) { this._change('MAR', 'mar', v, 0xFFFF); }
    setMDR(v) { this._change('MDR', 'mdr', v, 0xFF); }

    readRegister(index) { return this.r[index & 7]; }
    writeRegister(index, value) {
      index &= 7; value &= 0xFF;
      if (this.r[index] === value) return;
      const before = this.r[index];
      this.r[index] = value;
      this.record({ kind: 'register', name: 'R' + index, before, after: value, value });
    }
    readAddressRegister(index) { return this.x[index & 3]; }
    writeAddressRegister(index, value, rebase) {
      index &= 3; value &= 0xFFFF;
      if (index === SP && rebase !== false) this.stackBase = value;
      if (this.x[index] === value) return;
      const before = this.x[index];
      this.x[index] = value;
      this.record({ kind: 'register', name: XREG[index], before, after: value, value });
    }
    get sp() { return this.x[SP]; }

    readMemory(address, purpose) {
      address &= 0xFFFF;
      if (purpose === 'opcode') this.timing.onOpcodeFetch(address);
      else if (purpose === 'operand') this.timing.onOperandFetch(address);
      else this.timing.onDataRead(address, purpose);
      this.setMAR(address);
      const value = this.memory[address];
      if (purpose === 'opcode') this.record({ kind: 'fetch', address, value, name: 'opcode' });
      else this.record({ kind: 'read', address, value, name: purpose });
      this.setMDR(value);
      return value;
    }
    writeMemory(address, value, purpose) {
      address &= 0xFFFF; value &= 0xFF;
      this.timing.onDataWrite(address, purpose);
      this.setMAR(address);
      this.setMDR(value);
      this.memory[address] = value;
      this.record({ kind: 'write', address, value, name: purpose });
    }
    readMemoryWord(address, purpose) {
      const low = this.readMemory(address, purpose);
      const high = this.readMemory(address + 1, purpose);
      return low | (high << 8);
    }
    writeMemoryWord(address, value, purpose) {
      this.writeMemory(address, value & 0xFF, purpose);
      this.writeMemory(address + 1, (value >> 8) & 0xFF, purpose);
    }

    push(value) {
      const sp = this.x[SP];
      if (sp < 1) this.raiseFault('StackOverflow', 'Stack overflow: SP is already 0x0000');
      this.writeAddressRegister(SP, sp - 1, false);
      this.writeMemory(sp - 1, value, 'stack');
    }
    pop() {
      const sp = this.x[SP];
      if (sp >= this.stackBase) this.raiseFault('StackUnderflow', 'Stack underflow: the stack is empty');
      const value = this.readMemory(sp, 'stack');
      this.writeAddressRegister(SP, sp + 1, false);
      return value;
    }
    pushWord(value) {
      const sp = this.x[SP];
      if (sp < 2) this.raiseFault('StackOverflow', 'Stack overflow: no room for a 16 bit push');
      this.writeAddressRegister(SP, sp - 2, false);
      this.writeMemoryWord(sp - 2, value, 'stack');
    }
    popWord() {
      const sp = this.x[SP];
      if (sp + 2 > this.stackBase) this.raiseFault('StackUnderflow', 'Stack underflow: no 16 bit value on the stack');
      const value = this.readMemoryWord(sp, 'stack');
      this.writeAddressRegister(SP, sp + 2, false);
      return value;
    }
    adjustStackPointer(delta) {
      const sp = this.x[SP] + delta;
      if (sp < 0) this.raiseFault('StackOverflow', 'Stack overflow: the stack frame does not fit below 0x0000');
      if (sp > this.stackBase) this.raiseFault('StackUnderflow', 'Stack underflow: SP moved above the stack base');
      this.writeAddressRegister(SP, sp, false);
    }

    jump(condition, target, taken, conditional, kind) {
      this.record({ kind: 'jump', name: condition, address: target, value: taken ? 1 : 0 });
      this.timing.onJump(this._stepAddress, target, taken, conditional, kind);
      if (taken) this.setIP(target);
    }
    output(port, value) {
      this.timing.onPortAccess(port, false);
      this.record({ kind: 'output', address: port, value, name: 'port' });
      const writer = this.writers[port & 0xFF];
      if (writer) writer(value);
      else if (this.onOutput) this.onOutput(port, value);
    }
    inputPort(port) {
      this.timing.onPortAccess(port, true);
      const reader = this.readers[port & 0xFF];
      const value = reader ? reader() & 0xFF : 0xFF;
      this.record({ kind: 'input', address: port, value, name: 'port' });
      return value;
    }
    enterInterrupt(irq, software) {
      const vector = VECTOR_TABLE + (irq & 7) * 2;
      const handler = this.readMemoryWord(vector, 'vector');
      this.record({ kind: 'interrupt', name: 'IRQ' + irq, value: irq, address: handler, text: 'IRQ' + irq + ' -> 0x' + ISA.hex4(handler) });
      this.push(this.flags);
      this.pushWord(this.ip);
      this.setFlag('i', false);
      this.waiting = false;
      this.timing.onInterrupt();
      this.jump(software ? 'INT' : 'IRQ' + irq, handler, true, false, 'interrupt');
    }
    returnFromInterrupt() {
      const target = this.popWord();
      this.setFlags(this.pop());
      this.jump('IRET', target, true, false, 'ret');
    }
    halt() {
      if (this.flag('i')) { this.waiting = true; this.record({ kind: 'halt', name: 'HLT', text: 'waiting for an interrupt' }); }
      else { this.halted = true; this.record({ kind: 'halt', name: 'HLT' }); }
    }
    raiseFault(kind, message) { throw new ProcessorFault(kind, message); }
    chargeEffectiveAddress(kind) { this.timing.onEffectiveAddress(kind); }

    // ---- ALU (mirror of ArithmeticLogicUnit.cs) ----------------------------------------------

    _setFlags(zero, carry, negative, overflow) {
      let flags = this.flags & FLAG.i;
      if (zero) flags |= FLAG.z;
      if (carry) flags |= FLAG.c;
      if (negative) flags |= FLAG.n;
      if (overflow) flags |= FLAG.v;
      this.setFlags(flags);
    }
    _flags8(result, carry, overflow) { this._setFlags(result === 0, carry, (result & 0x80) !== 0, overflow); }
    _flags16(result, carry, overflow) { this._setFlags(result === 0, carry, (result & 0x8000) !== 0, overflow); }
    _finish(name, a, b, result, wide) {
      this.record({ kind: 'alu', name, before: a, after: b, value: result });
      this.timing.onAlu(name, !!wide);
      return result;
    }
    aluAdd(a, b, useCarry) {
      const carryIn = useCarry && this.flag('c') ? 1 : 0;
      const full = a + b + carryIn;
      const result = full & 0xFF;
      this._flags8(result, full > 0xFF, ((a ^ result) & (b ^ result) & 0x80) !== 0);
      return this._finish(useCarry ? 'ADC' : 'ADD', a, b, result);
    }
    aluSub(a, b, useBorrow, name) {
      const borrowIn = useBorrow && this.flag('c') ? 1 : 0;
      const full = a - b - borrowIn;
      const result = full & 0xFF;
      this._flags8(result, full < 0, ((a ^ b) & (a ^ result) & 0x80) !== 0);
      return this._finish(name, a, b, result);
    }
    aluLogic(name, a, b, result) { this._flags8(result, false, false); return this._finish(name, a, b, result); }
    aluShift(name, a, result, carry) { this._flags8(result, carry, false); return this._finish(name, a, 1, result); }
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
        case 'inc': { const result = (a + 1) & 0xFF; this._flags8(result, this.flag('c'), a === 0x7F); return this._finish('INC', a, 1, result); }
        case 'dec': { const result = (a - 1) & 0xFF; this._flags8(result, this.flag('c'), a === 0x80); return this._finish('DEC', a, 1, result); }
        case 'not': return this.aluLogic('NOT', a, 0, (~a) & 0xFF);
        case 'neg': { const result = (-a) & 0xFF; this._flags8(result, a !== 0, a === 0x80); return this._finish('NEG', a, 0, result); }
        case 'shl': return this.aluShift('SHL', a, (a << 1) & 0xFF, (a & 0x80) !== 0);
        case 'shr': return this.aluShift('SHR', a, a >> 1, (a & 0x01) !== 0);
        case 'rol': return this.aluShift('ROL', a, ((a << 1) | (a >> 7)) & 0xFF, (a & 0x80) !== 0);
        case 'ror': return this.aluShift('ROR', a, ((a >> 1) | (a << 7)) & 0xFF, (a & 0x01) !== 0);
        default: throw new Error('Unknown unary operation ' + op);
      }
    }
    aluAddress(op, a, b) {
      switch (op) {
        case 'add': { const full = a + b; const result = full & 0xFFFF; this._flags16(result, full > 0xFFFF, ((a ^ result) & (b ^ result) & 0x8000) !== 0); return this._finish('ADD16', a, b, result, true); }
        case 'sub': case 'cmp': { const full = a - b; const result = full & 0xFFFF; this._flags16(result, full < 0, ((a ^ b) & (a ^ result) & 0x8000) !== 0); return this._finish(op === 'cmp' ? 'CMP16' : 'SUB16', a, b, result, true); }
        case 'inc': { const result = (a + 1) & 0xFFFF; this._flags16(result, this.flag('c'), a === 0x7FFF); return this._finish('INC16', a, 1, result, true); }
        case 'dec': { const result = (a - 1) & 0xFFFF; this._flags16(result, this.flag('c'), a === 0x8000); return this._finish('DEC16', a, 1, result, true); }
        default: throw new Error('Unknown address operation ' + op);
      }
    }
    aluMultiply(a, b) {
      const product = a * b;
      this._flags8(product & 0xFF, product > 0xFF, product > 0xFF);
      this.record({ kind: 'alu', name: 'MUL', before: a, after: b, value: product });
      this.timing.onAlu('MUL', true);
      return product;
    }
    aluDivide(dividend, divisor) {
      if (divisor === 0) this.raiseFault('DivideByZero', 'DIV by zero');
      const quotient = Math.floor(dividend / divisor);
      if (quotient > 0xFF) this.raiseFault('DivideOverflow', 'DIV overflow: ' + dividend + ' / ' + divisor + ' = ' + quotient + ' does not fit in 8 bits');
      const remainder = dividend % divisor;
      this._flags8(quotient, false, false);
      this.record({ kind: 'alu', name: 'DIV', before: dividend, after: divisor, value: quotient });
      this.timing.onAlu('DIV', true);
      return { quotient, remainder };
    }

    // ---- addressing modes (mirror of Instruction.cs) -----------------------------------------

    effectiveAddress(slot, ops) {
      const o = slot.offset;
      let address;
      switch (slot.kind) {
        case 'direct': address = ops[o] | (ops[o + 1] << 8); break;
        case 'indirect': address = this.readRegister(ops[o]); break;
        case 'indexed': address = (ops[o] | (ops[o + 1] << 8)) + this.readRegister(ops[o + 2]); break;
        case 'xind': address = this.readAddressRegister(ops[o]); break;
        case 'xdisp': address = this.readAddressRegister(ops[o]) + ISA.signed8(ops[o + 1]); break;
        default: throw new Error('Operand kind ' + slot.kind + ' does not address memory');
      }
      this.chargeEffectiveAddress(slot.kind);
      return address & 0xFFFF;
    }
    readOperand(slot, ops) {
      switch (slot.kind) {
        case 'acc': return this.a;
        case 'reg': return this.readRegister(ops[slot.offset]);
        case 'imm': return ops[slot.offset];
        case 'flags': return this.flags;
        default:
          if (ISA.isMemoryKind(slot.kind)) return this.readMemory(this.effectiveAddress(slot, ops), 'data');
          throw new Error('Operand kind ' + slot.kind + ' is not an 8 bit operand');
      }
    }
    writeOperand(slot, ops, value) {
      switch (slot.kind) {
        case 'acc': this.setA(value); break;
        case 'reg': this.writeRegister(ops[slot.offset], value); break;
        case 'flags': this.setFlags(value); break;
        default:
          if (!ISA.isMemoryKind(slot.kind)) throw new Error('Operand kind ' + slot.kind + ' cannot be written as a byte');
          this.writeMemory(this.effectiveAddress(slot, ops), value, 'data');
      }
    }
    readOperand16(slot, ops) {
      const o = slot.offset;
      switch (slot.kind) {
        case 'xreg': return this.readAddressRegister(ops[o]);
        case 'imm16': case 'addr': return ops[o] | (ops[o + 1] << 8);
        default:
          if (ISA.isMemoryKind(slot.kind)) return this.readMemoryWord(this.effectiveAddress(slot, ops), 'data');
          throw new Error('Operand kind ' + slot.kind + ' is not a 16 bit operand');
      }
    }
    writeOperand16(slot, ops, value) {
      if (slot.kind === 'xreg') { this.writeAddressRegister(ops[slot.offset], value); return; }
      if (!ISA.isMemoryKind(slot.kind)) throw new Error('Operand kind ' + slot.kind + ' cannot be written as a word');
      this.writeMemoryWord(this.effectiveAddress(slot, ops), value, 'data');
    }

    // ---- instruction semantics (mirror of the Instructions/ folder) ---------------------------

    execute(instruction, ops) {
      const slots = instruction.syntax.operands;
      switch (instruction.family) {
        case 'nop': break;
        case 'hlt': this.halt(); break;
        case 'ei': this.setFlag('i', true); break;
        case 'di': this.setFlag('i', false); break;
        case 'iret': this.returnFromInterrupt(); break;
        case 'int': this.enterInterrupt(this.readOperand(slots[0], ops) & 7, true); break;
        case 'mov':
          if (instruction.wide) this.writeOperand16(slots[0], ops, this.readOperand16(slots[1], ops));
          else this.writeOperand(slots[0], ops, this.readOperand(slots[1], ops));
          break;
        case 'xchg': { const a = this.a; const r = this.readOperand(slots[1], ops); this.setA(r); this.writeOperand(slots[1], ops, a); break; }
        case 'lea': {
          const source = slots[1];
          const address = this.readAddressRegister(ops[source.offset]) + ISA.signed8(ops[source.offset + 1]);
          this.chargeEffectiveAddress(source.kind);
          this.writeAddressRegister(ops[slots[0].offset], address);
          break;
        }
        case 'push':
          if (ISA.isWideKind(slots[0].kind)) this.pushWord(this.readOperand16(slots[0], ops));
          else this.push(this.readOperand(slots[0], ops));
          break;
        case 'pop':
          if (ISA.isWideKind(slots[0].kind)) this.writeOperand16(slots[0], ops, this.popWord());
          else this.writeOperand(slots[0], ops, this.pop());
          break;
        case 'enter': {
          const locals = this.readOperand(slots[0], ops);
          this.pushWord(this.x[2]);
          this.writeAddressRegister(2, this.x[SP]);
          this.adjustStackPointer(-locals);
          break;
        }
        case 'leave':
          this.adjustStackPointer(this.x[2] - this.x[SP]);
          this.writeAddressRegister(2, this.popWord());
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
        case 'xalu': {
          const a = this.readOperand16(slots[0], ops);
          const b = slots.length === 1 ? 0 : slots[1].kind === 'acc' ? this.a : this.readOperand16(slots[1], ops);
          const result = this.aluAddress(instruction.op, a, b);
          if (instruction.op !== 'cmp') this.writeOperand16(slots[0], ops, result);
          break;
        }
        case 'mul': {
          const product = this.aluMultiply(this.a, this.readOperand(slots[0], ops));
          this.setA(product & 0xFF);
          this.writeRegister(7, product >> 8);
          break;
        }
        case 'div': {
          const dividend = (this.readRegister(7) << 8) | this.a;
          const { quotient, remainder } = this.aluDivide(dividend, this.readOperand(slots[0], ops));
          this.setA(quotient);
          this.writeRegister(7, remainder);
          break;
        }
        case 'jump':
          this.jump(instruction.mnemonic, this.readOperand16(slots[0], ops), this.conditionHolds(instruction.cond), instruction.cond !== 'always', 'jump');
          break;
        case 'jmpind':
          this.jump('JMP', this.readOperand16(slots[0], ops), true, false, 'indirect');
          break;
        case 'call': { const target = this.readOperand16(slots[0], ops); this.pushWord(this.ip); this.jump('CALL', target, true, false, 'call'); break; }
        case 'ret': this.jump('RET', this.popWord(), true, false, 'ret'); break;
        case 'loop': {
          const counter = (this.readOperand(slots[0], ops) - 1) & 0xFF;
          this.writeOperand(slots[0], ops, counter);
          this.jump('LOOP', this.readOperand16(slots[1], ops), counter !== 0, true, 'loop');
          break;
        }
        case 'flag': this.setFlag(instruction.flag, instruction.value); break;
        case 'string': this.executeString(instruction); break;
        case 'out': this.output(this.readOperand(slots[0], ops), this.a); break;
        case 'in': this.setA(this.inputPort(this.readOperand(slots[1], ops))); break;
        default: throw new Error('Unknown instruction family ' + instruction.family);
      }
    }

    executeString(instruction) {
      if (!instruction.rep) { this.stringStep(instruction.op); return; }
      while (this.readRegister(COUNT_REGISTER) !== 0) {
        this.stringStep(instruction.op);
        this.writeRegister(COUNT_REGISTER, this.readRegister(COUNT_REGISTER) - 1);
        if (instruction.op === 'compare' && !this.flag('z')) break;
        if (instruction.op === 'scan' && this.flag('z')) break;
      }
    }

    stringStep(op) {
      const si = this.x[0];
      const di = this.x[1];
      switch (op) {
        case 'load': this.setA(this.readMemory(si, 'data')); this.writeAddressRegister(0, si + 1); break;
        case 'store': this.writeMemory(di, this.a, 'data'); this.writeAddressRegister(1, di + 1); break;
        case 'move': this.writeMemory(di, this.readMemory(si, 'data'), 'data'); this.writeAddressRegister(0, si + 1); this.writeAddressRegister(1, di + 1); break;
        case 'compare': this.aluBinary('cmp', this.readMemory(si, 'data'), this.readMemory(di, 'data')); this.writeAddressRegister(0, si + 1); this.writeAddressRegister(1, di + 1); break;
        case 'scan': this.aluBinary('cmp', this.a, this.readMemory(di, 'data')); this.writeAddressRegister(1, di + 1); break;
        default: break;
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

    nextPendingInterrupt() {
      if (this.pending === 0) return -1;
      for (let irq = 0; irq < 8; irq++) if (this.pending & (1 << irq)) return irq;
      return -1;
    }

    /** Performs one step and returns its trace. Throws if the processor is halted. */
    step() {
      if (this.halted) throw new Error('The processor is halted. Reset it or load a new program before stepping.');
      const ops = [];
      this._ops = ops;
      this._stepCycles = 0;
      this._stepAddress = this.ip;
      let kind = 'instruction';
      let opcode = 0;
      const operandBytes = [];
      let disassembly = '?';

      try {
        const irq = this.nextPendingInterrupt();
        if (irq >= 0 && this.flag('i')) {
          kind = 'interrupt';
          this.phase = 'interrupt';
          this.pending &= ~(1 << irq);
          this.waiting = false;
          this.enterInterrupt(irq, false);
          disassembly = 'IRQ' + irq + ' -> 0x' + ISA.hex4(this.ip);
        } else if (this.waiting) {
          kind = 'wait';
          this.phase = 'waiting';
          const idle = this.timing.onIdle();
          this.record({ kind: 'wait', name: 'wait', value: idle, text: 'waiting for an interrupt' });
          disassembly = 'WAIT';
        } else {
          this.phase = 'fetch';
          opcode = this.readMemory(this.ip, 'opcode');
          this.setIR(opcode);
          this.setIP(this.ip + 1);

          this.phase = 'decode';
          const instruction = ISA.decode(opcode);
          if (!instruction) this.raiseFault('InvalidOpcode', 'Invalid opcode 0x' + ISA.hex2(opcode) + ' at address 0x' + ISA.hex4(this._stepAddress));
          for (let i = 0; i < instruction.syntax.operandBytes; i++) {
            operandBytes.push(this.readMemory(this.ip, 'operand'));
            this.setIP(this.ip + 1);
          }
          disassembly = ISA.format(instruction.syntax, operandBytes);
          this.record({ kind: 'decode', value: opcode, name: instruction.mnemonic, text: disassembly });

          this.phase = 'execute';
          this.record({ kind: 'execute', name: instruction.mnemonic });
          this.execute(instruction, operandBytes);
          this.timing.onInstructionComplete(instruction);
          this.instructions += 1;
          if (this.halted) this.phase = 'halted';
          else if (this.waiting) this.phase = 'waiting';
        }
      } catch (error) {
        if (!(error instanceof ProcessorFault)) { this._ops = null; throw error; }
        this.halted = true;
        this.waiting = false;
        this.fault = error.kind;
        this.faultMessage = error.message;
        this.phase = 'halted';
        this.record({ kind: 'fault', name: error.kind, text: error.message });
      } finally {
        this._ops = null;
      }

      const cycles = this._stepCycles;
      this.cycles += cycles;
      for (const d of this.devices) d.tick(cycles);

      const trace = {
        kind, address: this._stepAddress, bytes: kind === 'instruction' ? [opcode].concat(operandBytes) : [],
        disassembly, cycles, ops, state: this.getState(), timing: this.timing.snapshot(),
      };
      this.lastStep = trace;
      return trace;
    }

    /** Runs until HLT, a fault or the step budget is used up. */
    run(maxSteps) {
      maxSteps = maxSteps || 100000;
      let steps = 0;
      while (!this.halted && steps < maxSteps) { this.step(); steps++; }
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
  CPU.VECTOR_TABLE = VECTOR_TABLE;
  CPU.ProcessorFault = ProcessorFault;

  global.CISC.CPU = CPU;
})(typeof window !== 'undefined' ? window : globalThis);
