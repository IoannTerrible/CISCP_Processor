/*
 * Era timing models - JavaScript mirror of CISCProcessorLibrary/Timing/*.cs.
 * The processor calls the hooks as it executes; the model charges cycles and reports events.
 */
(function (global) {
  'use strict';

  const hex4 = (v) => (v & 0xFFFF).toString(16).toUpperCase().padStart(4, '0');

  class TimingModel {
    constructor(cpu) {
      this.cpu = cpu;
      this.statistics = {};
    }
    reset() { this.statistics = {}; }
    charge(cycles, eventName, text, address) {
      if (cycles > 0) this.cpu.addCycles(cycles);
      if (eventName) this.cpu.record({ kind: 'timing', name: eventName, text: text || eventName, value: cycles, address: address === undefined ? -1 : address });
    }
    count(statistic, amount) { this.statistics[statistic] = (this.statistics[statistic] || 0) + (amount === undefined ? 1 : amount); }
    get(statistic) { return this.statistics[statistic] || 0; }
    snapshot(extra) {
      return Object.assign({
        kind: this.kind, name: this.name, statistics: Object.assign({}, this.statistics),
        prefetchQueueAddress: -1, prefetchQueueLength: 0, prefetchQueueCapacity: 0,
        instructionCacheTags: [], dataCacheTags: [], cacheLineSize: 0, branchPredictorCounters: [],
      }, extra || {});
    }
    onEffectiveAddress() {}
    onInstructionComplete() {}
  }

  /** 1975: multi-cycle control unit, 8080 style T-states. */
  class ClassicTiming extends TimingModel {
    constructor(cpu) { super(cpu); this.kind = 'Classic1975'; this.name = '1975 multi-cycle (8080 style)'; }
    onOpcodeFetch() { this.count('bus cycles'); this.charge(4); }
    onOperandFetch() { this.count('bus cycles'); this.charge(3); }
    onDataRead() { this.count('bus cycles'); this.charge(3); }
    onDataWrite() { this.count('bus cycles'); this.charge(3); }
    onPortAccess() { this.count('bus cycles'); this.charge(3); }
    onEffectiveAddress(kind) { if (kind === 'indexed' || kind === 'xdisp') this.charge(2); }
    onAlu(operation) {
      this.count('ALU operations');
      switch (operation) {
        case 'MUL': this.charge(16); break;
        case 'DIV': this.charge(24); break;
        case 'INC': case 'DEC': case 'NEG': case 'NOT': case 'SHL': case 'SHR': case 'ROL': case 'ROR': this.charge(1); break;
        case 'ADD16': case 'SUB16': case 'CMP16': this.charge(2); break;
        case 'INC16': case 'DEC16': this.charge(1); break;
        default: break;
      }
    }
    onJump(from, target, taken, conditional, kind) {
      this.count(taken ? 'branches taken' : 'branches not taken');
      if (taken && kind === 'call') this.charge(1);
    }
    onInterrupt() { this.count('interrupts'); this.charge(11, 'interrupt', 'interrupt acknowledge cycle'); }
    onIdle() { this.charge(4); return 4; }
  }

  /** 1985: 8086 style bus interface unit with a 6 byte prefetch queue fed by 16 bit bus cycles. */
  class PrefetchTiming extends TimingModel {
    constructor(cpu) {
      super(cpu);
      this.kind = 'Prefetch1985';
      this.name = '1985 prefetch queue (8086 style)';
      this.reset();
    }
    reset() { super.reset(); this.queueHead = 0; this.queueLength = 0; this.fetchAddress = 0; this.busProgress = 0; }
    snapshot() { return super.snapshot({ prefetchQueueAddress: this.queueHead, prefetchQueueLength: this.queueLength, prefetchQueueCapacity: 6 }); }
    onOpcodeFetch(address) { this.fetchByte(address); }
    onOperandFetch(address) { this.fetchByte(address); }
    onDataRead() { this.busBusy(4); }
    onDataWrite() { this.busBusy(4); }
    onPortAccess() { this.busBusy(4); }
    onEffectiveAddress(kind) { if (kind === 'indexed' || kind === 'xdisp') this.internal(3); }
    onAlu(operation, wide) {
      this.count('ALU operations');
      this.internal(operation === 'MUL' ? 30 : operation === 'DIV' ? 40 : wide ? 3 : 2);
    }
    onJump(from, target, taken, conditional, kind) {
      this.count(taken ? 'branches taken' : 'branches not taken');
      if (!taken) return;
      this.count('queue flushes');
      this.flush(target);
      this.charge(0, 'queue flush', 'taken ' + kind + ': prefetch queue flushed, refetching from 0x' + hex4(target), target);
      this.internal(4);
    }
    onInterrupt() { this.count('interrupts'); this.charge(40, 'interrupt', 'interrupt acknowledge, queue flushed'); this.flush(this.cpu.ip); }
    onIdle() { this.internal(4); return 4; }
    fetchByte(address) {
      if (this.queueLength > 0 && address === this.queueHead) {
        this.count('queue hits');
        this.queueLength--;
        this.queueHead = (this.queueHead + 1) & 0xFFFF;
        this.charge(1, 'queue hit', 'byte 0x' + hex4(address) + ' taken from the prefetch queue (' + this.queueLength + ' left)', address);
        this.internal(0);
        return;
      }
      if (address !== this.fetchAddress || this.queueLength > 0) this.flush(address);
      const wait = 4 - this.busProgress;
      this.count('queue misses');
      this.count('bus cycles');
      this.completeFetch();
      this.busProgress = 0;
      this.queueLength--;
      this.queueHead = (this.queueHead + 1) & 0xFFFF;
      this.charge(wait, 'queue miss', 'byte 0x' + hex4(address) + ': waited ' + wait + ' cycles for the bus interface unit', address);
    }
    internal(cycles) {
      if (cycles > 0) this.charge(cycles);
      this.busProgress += cycles;
      while (this.busProgress >= 4 && this.queueLength < 6) {
        this.busProgress -= 4;
        this.count('bus cycles');
        this.count('words prefetched');
        this.completeFetch();
      }
      if (this.queueLength >= 6) this.busProgress = 0;
      else if (this.busProgress >= 4) this.busProgress = 3;
    }
    busBusy(cycles) { this.count('bus cycles'); this.charge(cycles); }
    completeFetch() {
      let bytes = (this.fetchAddress & 1) !== 0 ? 1 : 2;
      bytes = Math.min(bytes, 6 - this.queueLength);
      if (bytes <= 0) bytes = 1;
      this.queueLength += bytes;
      this.fetchAddress = (this.fetchAddress + bytes) & 0xFFFF;
    }
    flush(address) { this.queueLength = 0; this.queueHead = address; this.fetchAddress = address; this.busProgress = 0; }
  }

  /** 1995: Pentium style pipeline with instruction / data caches and a branch target buffer. */
  class PipelinedTiming extends TimingModel {
    constructor(cpu) {
      super(cpu);
      this.kind = 'Pipelined1995';
      this.name = '1995 pipeline + caches (Pentium style)';
      this.reset();
    }
    reset() {
      super.reset();
      this.instructionTags = new Array(16).fill(-1);
      this.dataTags = new Array(16).fill(-1);
      this.predictor = new Array(16).fill(1);
      this.targets = new Array(16).fill(-1);
      this.lastFetchLine = -1;
    }
    snapshot() {
      return super.snapshot({ instructionCacheTags: this.instructionTags.slice(), dataCacheTags: this.dataTags.slice(), cacheLineSize: 16, branchPredictorCounters: this.predictor.slice() });
    }
    onOpcodeFetch(address) { this.lastFetchLine = -1; this.access(this.instructionTags, address, 'I-cache', 'instruction cache'); this.lastFetchLine = Math.floor(address / 16); }
    onOperandFetch(address) {
      if (Math.floor(address / 16) === this.lastFetchLine) return;
      this.access(this.instructionTags, address, 'I-cache', 'instruction cache');
      this.lastFetchLine = Math.floor(address / 16);
    }
    onDataRead(address) { this.access(this.dataTags, address, 'D-cache', 'data cache'); }
    onDataWrite(address) { this.access(this.dataTags, address, 'D-cache', 'data cache'); }
    onPortAccess(port) { this.count('port transfers'); this.charge(8, 'I/O', 'uncached port transfer', port); }
    onAlu(operation) {
      this.count('ALU operations');
      if (operation === 'MUL') this.charge(9, 'multi-cycle', 'multiplier busy');
      else if (operation === 'DIV') this.charge(39, 'multi-cycle', 'divider busy');
    }
    onJump(from, target, taken, conditional, kind) {
      const slot = (from >> 1) & 15;
      const targetKnown = this.targets[slot] === target;
      const predictedTaken = conditional ? this.predictor[slot] >= 2 : targetKnown;
      const correct = predictedTaken === taken && (!taken || targetKnown);
      if (taken) { if (this.predictor[slot] < 3) this.predictor[slot]++; this.targets[slot] = target; }
      else if (this.predictor[slot] > 0) this.predictor[slot]--;
      if (correct) { this.count('branches predicted'); this.charge(0, 'predicted', kind + ' predicted ' + (taken ? 'taken' : 'not taken'), target); }
      else { this.count('branches mispredicted'); this.charge(3, 'mispredicted', kind + ' mispredicted, pipeline flushed', target); }
    }
    onInstructionComplete() { this.count('instructions issued'); this.charge(1); }
    onInterrupt() { this.count('interrupts'); this.charge(20, 'interrupt', 'interrupt: pipeline flushed'); }
    onIdle() { this.charge(1); return 1; }
    access(tags, address, cache, description) {
      const line = Math.floor(address / 16) % 16;
      const tag = Math.floor(address / 256);
      if (tags[line] === tag) { this.count(cache + ' hits'); this.charge(0, cache + ' hit', description + ' hit, line ' + line, address); }
      else { this.count(cache + ' misses'); tags[line] = tag; this.charge(8, cache + ' miss', description + ' miss, line ' + line + ' filled from memory', address); }
    }
  }

  const MICROARCHITECTURES = [
    { id: 'Classic1975', year: 1975, name: '1975 multi-cycle (8080 style)', description: 'Every memory access is a separate machine cycle: 4 T-states for the opcode fetch, 3 for each other byte. The control unit does one thing at a time, so a program\'s cycle count is the sum of its bus cycles.' },
    { id: 'Prefetch1985', year: 1985, name: '1985 prefetch queue (8086 style)', description: 'A bus interface unit keeps the 16 bit bus busy reading the next instruction words into a 6 byte queue while the execution unit works, so straight line code never waits for memory. Taken jumps flush the queue.' },
    { id: 'Pipelined1995', year: 1995, name: '1995 pipeline + caches (Pentium style)', description: 'Fetch, decode, execute, memory and write-back overlap, so one simple instruction retires per cycle. Instruction and data caches answer in one cycle; a miss costs the memory latency. A branch target buffer guesses jumps and a wrong guess flushes the pipeline.' },
  ];

  function create(kind, cpu) {
    switch (kind) {
      case 'Prefetch1985': return new PrefetchTiming(cpu);
      case 'Pipelined1995': return new PipelinedTiming(cpu);
      default: return new ClassicTiming(cpu);
    }
  }

  global.CISC = global.CISC || {};
  global.CISC.Timing = { create, MICROARCHITECTURES, ClassicTiming, PrefetchTiming, PipelinedTiming };
})(typeof window !== 'undefined' ? window : globalThis);
