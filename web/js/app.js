/*
 * CISC-16 Processor Visualizer - user interface and animation player.
 * The player consumes the micro-operation trace produced by CPU.step() (identical in shape to the
 * StepTrace of the .NET library) and turns every bus transfer, ALU operation, register write, cache
 * lookup and interrupt into an animation. The engine behind the trace is swappable (see the engines).
 */
(function () {
  'use strict';

  const { ISA, CPU, Assembler, SAMPLES, Timing, I18N } = window.CISC;
  const t = (key, params) => I18N.t(key, params);
  const $ = (id) => document.getElementById(id);
  const hex2 = ISA.hex2;
  const hex4 = ISA.hex4;
  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
  const nextFrame = () => new Promise((resolve) => requestAnimationFrame(() => resolve()));
  const WIDE = new Set(['SI', 'DI', 'BP', 'SP', 'IP', 'MAR']);
  const ERA_LABEL = { Classic1975: '1975', Prefetch1985: '1985', Pipelined1995: '1995' };

  // ---------------------------------------------------------------- engine ----

  /** Runs the program on the JavaScript mirror of the processor, in the browser. */
  class LocalEngine {
    constructor() {
      this.cpu = new CPU('Classic1975');
      this.id = 'js';
      this.name = 'JavaScript';
    }

    /** Assembles the source and loads it. Throws an AssemblyError (with .line) when the source is invalid. */
    async load(source, options) {
      const program = Assembler.assemble(source);
      this.cpu.microarchitecture = options.microarchitecture;
      this.cpu.loadProgram(program);
      if (options.input) this.cpu.input.enqueue(options.input);
      return { program, state: this.cpu.getState(), timing: this.cpu.timing.snapshot() };
    }

    get halted() { return this.cpu.halted; }

    get ip() { return this.cpu.ip; }

    get busy() { return this.cpu.waiting || this.cpu.pending !== 0; }

    async step() { return this.cpu.step(); }

    /** Runs up to `count` steps without animation, stopping in front of any address in `stopAt`. */
    async batch(count, stopAt) {
      const traces = [];
      while (traces.length < count && !this.cpu.halted) {
        if (traces.length > 0 && !this.busy && stopAt.has(this.cpu.ip)) break;
        traces.push(this.cpu.step());
      }
      return traces;
    }

    async sendInput(text) { this.cpu.input.enqueue(text); return this.cpu.input.peekText(); }
  }

  /** Runs the program on the .NET implementation through the CISCProcessorWeb API. */
  class RemoteEngine {
    constructor(baseUrl, info) {
      this.base = baseUrl;
      this.id = 'dotnet';
      this.name = info.engine || '.NET';
      this.session = null;
      this._halted = false;
      this._ip = 0;
      this._busy = false;
    }

    static async detect(baseUrl) {
      try {
        const response = await fetch(baseUrl + 'api/info', { cache: 'no-store' });
        if (!response.ok) return null;
        const info = await response.json();
        return info && info.engine ? info : null;
      } catch (e) {
        return null;
      }
    }

    async request(path, body) {
      let response;
      try {
        response = await fetch(this.base + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body || {}) });
      } catch (e) {
        throw new Error('The .NET engine is not reachable (' + e.message + ').');
      }
      const payload = response.status === 204 ? {} : await response.json().catch(() => ({}));
      if (!response.ok) {
        const error = new Error(payload.message || ('The server answered ' + response.status + '.'));
        if (payload.line) error.line = payload.line;
        throw error;
      }
      return payload;
    }

    async load(source, options) {
      const result = await this.request('api/sessions', { source, microarchitecture: options.microarchitecture, input: options.input || '' });
      this.session = result.id;
      this.track(result.state);
      return { program: result.program, state: result.state, timing: result.timing };
    }

    track(state) {
      this._halted = state.halted;
      this._ip = state.ip;
      this._busy = state.waiting || state.pending !== 0;
    }

    get halted() { return this._halted; }

    get ip() { return this._ip; }

    get busy() { return this._busy; }

    async step() {
      const result = await this.request('api/sessions/' + this.session + '/step');
      this.track(result.trace.state);
      return result.trace;
    }

    async batch(count, stopAt) {
      const result = await this.request('api/sessions/' + this.session + '/run', { maxSteps: count, stopAt: Array.from(stopAt) });
      if (result.traces.length) this.track(result.traces[result.traces.length - 1].state);
      return result.traces;
    }

    async sendInput(text) {
      const result = await this.request('api/sessions/' + this.session + '/input', { text });
      return result.queued;
    }
  }

  // ----------------------------------------------------------------- state ----

  const DELAYS = [0, 520, 340, 220, 140, 90, 55, 30, 14, 5, 0];

  const ui = {
    engine: new LocalEngine(),
    engines: {},
    program: null,
    modified: true,
    running: false,
    busy: false,
    runPromise: null,
    speed: 5,
    microarchitecture: 'Classic1975',
    breakpoints: new Set(),
    memory: new Uint8Array(65536),
    cellKind: new Uint8Array(65536),      // 0 none, 1 code, 2 data
    page: 0,
    follow: true,
    state: null,
    timing: null,
    traceRows: 0,
    outputCount: 0,
    currentRow: null,
    nextRow: null,
    currentRange: null,
    inputQueue: '',
    rowByAddress: new Map(),
    lastStatus: { kind: 'ready', key: 'status.initial', params: null, text: null },
  };

  const el = {
    source: $('source'), gutter: $('gutter'), error: $('assemble-error'), stats: $('program-stats'),
    listing: $('listing'), sampleSelect: $('sample-select'), archSelect: $('arch-select'), speed: $('speed'), speedLabel: $('speed-label'),
    btnAssemble: $('btn-assemble'), btnRun: $('btn-run'), btnStep: $('btn-step'), btnReset: $('btn-reset'),
    btnReference: $('btn-reference'), btnCloseReference: $('btn-close-reference'), btnTheme: $('btn-theme'),
    btnClearTrace: $('btn-clear-trace'),
    stage: $('stage'), die: $('die'), overlay: $('overlay'), memory: $('memory'), memoryPage: $('memory-page'),
    btnPagePrev: $('btn-page-prev'), btnPageNext: $('btn-page-next'), btnPageIp: $('btn-page-ip'), btnPageSp: $('btn-page-sp'), followBus: $('follow-bus'),
    decodedText: $('decoded-text'), clockLed: $('clock-led'),
    statCycles: $('stat-cycles'), statInstructions: $('stat-instructions'), statLastCycles: $('stat-last-cycles'),
    statIp: $('stat-ip'), statSp: $('stat-sp'), statPhase: $('stat-phase'), statEra: $('stat-era'), statEngine: $('stat-engine'),
    alu: $('alu'), aluA: $('alu-a').querySelector('.port-value'), aluB: $('alu-b').querySelector('.port-value'),
    aluOp: $('alu-op'), aluResult: $('alu-result'),
    laneAddr: $('lane-addr'), laneData: $('lane-data'),
    consoleText: $('console-text'), trace: $('trace'),
    statusBadge: $('status-badge'), statusText: $('status-text'),
    reference: $('reference'), referenceTable: $('reference-table'),
    engineField: $('engine-field'), engineSelect: $('engine-select'),
    eraName: $('era-name'), eraClassic: $('era-classic'), eraPrefetch: $('era-prefetch'), eraPipelined: $('era-pipelined'),
    tstates: $('tstates'), queueAddress: $('queue-address'), queueCells: $('queue-cells'), icache: $('icache'), dcache: $('dcache'), predictor: $('predictor'),
    eraStats: $('era-stats'), eraEvent: $('era-event'),
    irqLeds: $('irq-leds'), flagIDevice: $('flag-i-device'), timerPeriod: $('timer-period'), timerTicks: $('timer-ticks'), timerLed: $('timer-led'), timerState: $('timer-state'),
    inputText: $('input-text'), btnSendInput: $('btn-send-input'), inputQueue: $('input-queue'),
    btnGuide: $('btn-guide'), btnCloseGuide: $('btn-close-guide'), guide: $('guide'), guideNav: $('guide-nav'), guideContent: $('guide-content'),
  };

  const regEl = (name) => $('reg-' + name);
  const cellEl = (address) => $('cell-' + (address & 0xFF));
  const pageOf = (address) => (address >> 8) & 0xFF;
  const delay = () => DELAYS[ui.speed];

  // ------------------------------------------------------------- building ----

  function buildMemoryGrid() {
    const frag = document.createDocumentFragment();
    const corner = document.createElement('div');
    corner.className = 'cell hdr';
    frag.appendChild(corner);
    for (let col = 0; col < 16; col++) {
      const h = document.createElement('div');
      h.className = 'cell hdr';
      h.textContent = hex2(col).slice(1);
      frag.appendChild(h);
    }
    for (let row = 0; row < 16; row++) {
      const h = document.createElement('div');
      h.className = 'cell hdr';
      h.id = 'row-' + row;
      h.textContent = hex4(row * 16);
      frag.appendChild(h);
      for (let col = 0; col < 16; col++) {
        const cell = document.createElement('div');
        cell.className = 'cell';
        cell.id = 'cell-' + (row * 16 + col);
        cell.textContent = '00';
        frag.appendChild(cell);
      }
    }
    el.memory.appendChild(frag);
  }

  function cellTitle(address, value) {
    const chr = value >= 32 && value < 127 ? " '" + String.fromCharCode(value) + "'" : '';
    return '[0x' + hex4(address) + '] = 0x' + hex2(value) + ' = ' + value + chr;
  }

  function buildSampleSelect() {
    const selected = el.sampleSelect.value;
    el.sampleSelect.innerHTML = '';
    for (const sample of SAMPLES) {
      const option = document.createElement('option');
      option.value = sample.id;
      option.textContent = I18N.sampleName(sample);
      option.title = I18N.sampleDescription(sample);
      el.sampleSelect.appendChild(option);
    }
    const custom = document.createElement('option');
    custom.value = '';
    custom.textContent = t('sample.custom');
    el.sampleSelect.appendChild(custom);
    el.sampleSelect.value = selected;
  }

  function buildReference() {
    const families = [
      ['family.control', (i) => i.opcode < 0x10], ['family.move', (i) => i.opcode >= 0x10 && i.opcode < 0x30],
      ['family.stack', (i) => i.opcode >= 0x30 && i.opcode < 0x40], ['family.arith', (i) => i.opcode >= 0x40 && i.opcode < 0x60],
      ['family.logic', (i) => i.opcode >= 0x60 && i.opcode < 0x70], ['family.flow', (i) => i.opcode >= 0x70 && i.opcode < 0x80],
      ['family.flags', (i) => i.opcode >= 0x80 && i.opcode < 0x90], ['family.string', (i) => i.opcode >= 0x90 && i.opcode < 0xA0],
      ['family.io', (i) => i.opcode >= 0xF0],
    ];
    el.referenceTable.innerHTML = '';
    const table = document.createElement('table');
    table.innerHTML = '<thead><tr><th></th><th></th><th></th><th></th></tr></thead>';
    const heads = table.querySelectorAll('th');
    heads[0].textContent = t('ref.op'); heads[1].textContent = t('ref.syntax'); heads[2].textContent = t('ref.bytes'); heads[3].textContent = t('ref.description');
    const body = document.createElement('tbody');
    for (const [name, filter] of families) {
      const head = document.createElement('tr');
      head.className = 'family';
      head.innerHTML = '<td colspan="4"></td>';
      head.firstChild.textContent = t(name);
      body.appendChild(head);
      for (const i of ISA.all.filter(filter)) {
        const row = document.createElement('tr');
        row.innerHTML = '<td class="op"></td><td class="syn"></td><td class="num"></td><td></td>';
        row.children[0].textContent = hex2(i.opcode);
        row.children[1].textContent = i.syntax.template;
        row.children[2].textContent = i.length;
        row.children[3].textContent = I18N.instructionDescription(i);
        body.appendChild(row);
      }
    }
    table.appendChild(body);
    el.referenceTable.appendChild(table);
  }

  function buildEraWidgets() {
    for (let i = 0; i < 6; i++) {
      const q = document.createElement('div');
      q.className = 'qcell';
      q.textContent = '--';
      el.queueCells.appendChild(q);
    }
    for (const container of [el.icache, el.dcache]) {
      for (let i = 0; i < 16; i++) {
        const line = document.createElement('div');
        line.className = 'line';
        line.textContent = '--';
        line.title = 'line ' + i;
        container.appendChild(line);
      }
    }
    for (let i = 0; i < 16; i++) {
      const line = document.createElement('div');
      line.className = 'line';
      line.title = 'predictor entry ' + i;
      line.appendChild(document.createElement('i'));
      el.predictor.appendChild(line);
    }
    for (let i = 0; i < 8; i++) {
      const led = document.createElement('span');
      led.className = 'led';
      led.id = 'irq-' + i;
      led.title = 'IRQ ' + i;
      el.irqLeds.appendChild(led);
    }
  }

  // --------------------------------------------------------------- editor ----

  function updateGutter(errorLine) {
    const lines = el.source.value.split('\n').length;
    let text = '';
    for (let i = 1; i <= lines; i++) text += (i === errorLine ? '<span class="err">' + i + '</span>' : i) + '\n';
    el.gutter.innerHTML = text;
    el.gutter.scrollTop = el.source.scrollTop;
  }

  function showError(error) {
    el.error.hidden = false;
    el.error.textContent = error.message;
    updateGutter(error.line);
    setStatusText('error', error.message);
  }

  function clearError() {
    el.error.hidden = true;
    el.error.textContent = '';
    updateGutter();
  }

  function loadOptions() {
    return { microarchitecture: ui.microarchitecture, input: el.inputText.value };
  }

  async function assemble() {
    clearError();
    ui.busy = true;
    try {
      const { program, state, timing } = await ui.engine.load(el.source.value, loadOptions());
      ui.program = program;
      ui.modified = false;
      el.stats.textContent = t('stats.program', { bytes: program.length, entry: hex4(program.entryPoint), symbols: Object.keys(program.symbols).length });
      renderListing(program);
      loadMemoryImage(program);
      resetMachineView(state, timing);
      setStatusKey('ready', 'status.assembled');
      return true;
    } catch (error) {
      ui.program = null;
      if (error.line) showError(error);
      else setStatusText('error', error.message);
      return false;
    } finally {
      ui.busy = false;
    }
  }

  /** Reloads the current (already assembled) program into the engine, e.g. after HLT or Reset. */
  async function reloadProgram() {
    const { program, state, timing } = await ui.engine.load(el.source.value, loadOptions());
    ui.program = program;
    loadMemoryImage(program);
    resetMachineView(state, timing);
  }

  function loadMemoryImage(program) {
    ui.memory.fill(0);
    ui.cellKind.fill(0);
    for (const line of program.listing) {
      for (let i = 0; i < line.bytes.length; i++) {
        const address = (line.address + i) & 0xFFFF;
        ui.memory[address] = line.bytes[i];
        ui.cellKind[address] = line.isInstruction ? 1 : 2;
      }
    }
  }

  function resetMachineView(state, timing) {
    ui.state = state;
    ui.timing = timing;
    ui.inputQueue = el.inputText.value || '';
    ui.currentRange = null;
    ui.page = pageOf(state.ip);
    renderPage();
    renderRegisters(state, false);
    renderFlags(state.flags, false);
    renderDevices(state, false);
    renderEra(timing, null);
    setPhase('idle');
    el.decodedText.textContent = '-';
    el.aluA.textContent = '--';
    el.aluB.textContent = '--';
    el.aluOp.textContent = t('alu.idle');
    el.aluResult.textContent = '--';
    el.die.classList.remove('halted', 'fault', 'running');
    el.consoleText.textContent = '';
    ui.outputCount = 0;
    el.trace.innerHTML = '';
    ui.traceRows = 0;
    highlightListing(-1, state.ip);
    renderStats(state, 0);
  }

  // ------------------------------------------------------------- listing ----

  function renderListing(program) {
    el.listing.innerHTML = '';
    ui.rowByAddress = new Map();
    const frag = document.createDocumentFragment();
    for (const line of program.listing) {
      const row = document.createElement('div');
      row.className = 'lrow' + (line.isData ? ' data' : '') + (line.isInstruction ? ' code' : '');
      row.setAttribute('role', 'row');
      const addr = document.createElement('span');
      addr.className = 'laddr';
      addr.textContent = line.bytes.length ? hex4(line.address) : '';
      const bytes = document.createElement('span');
      bytes.className = 'lbytes';
      bytes.textContent = line.bytes.map(hex2).join(' ');
      const src = document.createElement('span');
      src.className = 'lsrc';
      src.appendChild(highlightSource(line.source));
      row.append(addr, bytes, src);
      if (line.isInstruction) {
        row.dataset.address = line.address;
        if (ui.breakpoints.has(line.address)) row.classList.add('bp');
        addr.title = 'Toggle breakpoint at 0x' + hex4(line.address);
        addr.addEventListener('click', () => toggleBreakpoint(line.address, row));
        ui.rowByAddress.set(line.address, row);
      }
      frag.appendChild(row);
    }
    el.listing.appendChild(frag);
  }

  function highlightSource(source) {
    const frag = document.createDocumentFragment();
    const comment = source.indexOf(';');
    let code = comment >= 0 ? source.slice(0, comment) : source;
    const label = /^(\s*[A-Za-z_][A-Za-z0-9_]*\s*:)/.exec(code);
    if (label) {
      const span = document.createElement('span');
      span.className = 'label';
      span.textContent = label[1];
      frag.appendChild(span);
      code = code.slice(label[1].length);
    }
    frag.appendChild(document.createTextNode(code));
    if (comment >= 0) {
      const span = document.createElement('span');
      span.className = 'comment';
      span.textContent = source.slice(comment);
      frag.appendChild(span);
    }
    return frag;
  }

  function toggleBreakpoint(address, row) {
    if (ui.breakpoints.has(address)) { ui.breakpoints.delete(address); row.classList.remove('bp'); }
    else { ui.breakpoints.add(address); row.classList.add('bp'); }
  }

  function highlightListing(currentAddress, nextAddress) {
    if (ui.currentRow) ui.currentRow.classList.remove('current');
    if (ui.nextRow) ui.nextRow.classList.remove('next');
    ui.currentRow = ui.rowByAddress.get(currentAddress) || null;
    ui.nextRow = ui.rowByAddress.get(nextAddress) || null;
    if (ui.currentRow) {
      ui.currentRow.classList.add('current');
      scrollIntoView(el.listing, ui.currentRow);
    }
    if (ui.nextRow && ui.nextRow !== ui.currentRow) ui.nextRow.classList.add('next');
  }

  function scrollIntoView(container, row) {
    const top = row.offsetTop - container.offsetTop;
    if (top < container.scrollTop || top + row.offsetHeight > container.scrollTop + container.clientHeight) {
      container.scrollTop = top - container.clientHeight / 2;
    }
  }

  // ------------------------------------------------------------ rendering ----

  /** Renders every cell of the current memory page from the memory image and the processor state. */
  function renderPage() {
    const base = ui.page << 8;
    el.memoryPage.textContent = hex2(ui.page);
    for (const cell of el.memory.querySelectorAll('.flash-read, .flash-write')) cell.classList.remove('flash-read', 'flash-write');
    for (let row = 0; row < 16; row++) $('row-' + row).textContent = hex4(base + row * 16);
    for (let offset = 0; offset < 256; offset++) renderCell(base + offset);
  }

  function renderCell(address) {
    if (pageOf(address) !== ui.page) return;
    const cell = cellEl(address);
    const value = ui.memory[address];
    const s = ui.state;
    cell.textContent = hex2(value);
    cell.title = cellTitle(address, value);
    cell.classList.toggle('nonzero', value !== 0);
    cell.classList.toggle('code', ui.cellKind[address] === 1);
    cell.classList.toggle('data', ui.cellKind[address] === 2);
    cell.classList.toggle('vector', address >= CPU.VECTOR_TABLE && ui.cellKind[address] !== 1);
    cell.classList.toggle('stack', s ? address >= s.sp && address < CPU.INITIAL_SP : false);
    cell.classList.toggle('sp', s ? address === s.sp : false);
    cell.classList.toggle('current', ui.currentRange ? address >= ui.currentRange[0] && address < ui.currentRange[1] : false);
  }

  function showPage(page, flip) {
    page &= 0xFF;
    if (page === ui.page) return;
    ui.page = page;
    renderPage();
    if (flip) restartAnimation(el.memory, 'page-flip');
  }

  /** Makes sure the page holding `address` is visible when "follow" is on; returns the cell element or null. */
  function cellFor(address) {
    if (pageOf(address) !== ui.page) {
      if (!ui.follow) return null;
      showPage(pageOf(address), true);
    }
    return cellEl(address);
  }

  function markCurrentCells(address, length) {
    const previous = ui.currentRange;
    ui.currentRange = length > 0 ? [address, address + length] : null;
    if (previous) for (let a = previous[0]; a < previous[1]; a++) renderCell(a);
    if (ui.currentRange) {
      if (ui.follow) showPage(pageOf(address), true);
      for (let a = ui.currentRange[0]; a < ui.currentRange[1]; a++) renderCell(a);
    }
  }

  function renderStack(before, after) {
    if (before === after) return;
    const lo = Math.min(before, after);
    const hi = Math.max(before, after);
    for (let a = lo; a <= hi && a < 65536; a++) renderCell(a);
  }

  function setRegister(name, value, flash) {
    const reg = regEl(name);
    if (!reg) return;
    reg.querySelector('.reg-value').textContent = WIDE.has(name) ? hex4(value) : hex2(value);
    reg.title = name + ' = 0x' + (WIDE.has(name) ? hex4(value) : hex2(value)) + ' = ' + value;
    if (flash) restartAnimation(reg, 'flash');
  }

  function renderRegisters(state, flash) {
    setRegister('A', state.a, flash);
    for (let i = 0; i < 8; i++) setRegister('R' + i, state.r[i], flash);
    setRegister('SI', state.si, flash);
    setRegister('DI', state.di, flash);
    setRegister('BP', state.bp, flash);
    setRegister('SP', state.sp, flash);
    setRegister('IP', state.ip, flash);
    setRegister('IR', state.ir, flash);
    setRegister('MAR', state.mar, flash);
    setRegister('MDR', state.mdr, flash);
  }

  function renderFlags(flags, animate) {
    const names = { z: 'Zero', c: 'Carry', n: 'Negative', v: 'Overflow', i: 'Interrupts enabled' };
    for (const name of ['z', 'c', 'n', 'v', 'i']) {
      const flag = $('flag-' + name);
      const on = (flags & CPU.FLAG[name]) !== 0;
      const was = flag.classList.contains('on');
      flag.classList.toggle('on', on);
      flag.title = names[name] + ' = ' + (on ? 1 : 0);
      if (animate && was !== on) restartAnimation(flag, 'changed');
    }
    el.flagIDevice.classList.toggle('on', (flags & CPU.FLAG.i) !== 0);
  }

  function renderDevices(state, animate) {
    for (let i = 0; i < 8; i++) $('irq-' + i).classList.toggle('on', (state.pending & (1 << i)) !== 0);
    el.timerPeriod.textContent = state.timerPeriod;
    el.timerTicks.textContent = state.timerTicks;
    el.timerLed.classList.toggle('on', state.timerEnabled);
    el.timerState.textContent = state.timerEnabled ? t('dev.running') : t('dev.off');
    el.inputQueue.textContent = ui.inputQueue.length ? JSON.stringify(ui.inputQueue) : t('dev.empty');
    void animate;
  }

  function renderStats(state, lastCycles) {
    el.statCycles.textContent = state.cycles;
    el.statInstructions.textContent = state.instructions;
    el.statLastCycles.textContent = lastCycles;
    el.statIp.textContent = hex4(state.ip);
    el.statSp.textContent = hex4(state.sp);
    el.statPhase.textContent = t('phase.name.' + state.phase);
    el.statEra.textContent = ERA_LABEL[ui.microarchitecture] || ui.microarchitecture;
  }

  function setPhase(phase) {
    const order = ['fetch', 'decode', 'execute'];
    const index = order.indexOf(phase);
    for (let i = 0; i < order.length; i++) {
      const p = $('phase-' + order[i]);
      p.classList.toggle('active', i === index);
      p.classList.toggle('done', index >= 0 && i < index);
    }
    $('phase-interrupt').classList.toggle('active', phase === 'interrupt');
    $('phase-waiting').classList.toggle('active', phase === 'waiting');
    el.statPhase.textContent = t('phase.name.' + phase);
  }

  /** Draws the micro-architecture block from a timing snapshot. */
  function renderEra(timing, event) {
    if (!timing) return;
    ui.timing = timing;
    el.eraName.textContent = t('era.name.' + timing.kind);
    el.eraClassic.hidden = timing.kind !== 'Classic1975';
    el.eraPrefetch.hidden = timing.kind !== 'Prefetch1985';
    el.eraPipelined.hidden = timing.kind !== 'Pipelined1995';
    const stats = timing.statistics || {};
    el.eraStats.innerHTML = '';
    for (const key of Object.keys(stats).sort()) {
      const item = document.createElement('span');
      const b = document.createElement('b');
      b.textContent = stats[key];
      item.append(I18N.statName(key) + ' ', b);
      el.eraStats.appendChild(item);
    }
    if (timing.kind === 'Classic1975') {
      const bus = stats['bus cycles'] || 0;
      const shown = Math.min(bus, 48);
      el.tstates.innerHTML = '';
      for (let i = 0; i < shown; i++) { const t = document.createElement('i'); t.className = 'bus'; el.tstates.appendChild(t); }
      if (bus > shown) { const more = document.createElement('span'); more.className = 'era-text'; more.textContent = ' +' + (bus - shown); el.tstates.appendChild(more); }
    } else if (timing.kind === 'Prefetch1985') {
      el.queueAddress.textContent = timing.prefetchQueueLength ? '0x' + hex4(timing.prefetchQueueAddress) + ' .. ' + hex4(timing.prefetchQueueAddress + timing.prefetchQueueLength - 1) : t('era.queue.empty');
      const cells = el.queueCells.children;
      for (let i = 0; i < cells.length; i++) {
        const full = i < timing.prefetchQueueLength;
        cells[i].classList.toggle('full', full);
        cells[i].textContent = full ? hex2(ui.memory[(timing.prefetchQueueAddress + i) & 0xFFFF]) : '--';
      }
    } else {
      renderCacheLines(el.icache, timing.instructionCacheTags);
      renderCacheLines(el.dcache, timing.dataCacheTags);
      const counters = timing.branchPredictorCounters || [];
      for (let i = 0; i < el.predictor.children.length; i++) {
        const line = el.predictor.children[i];
        const c = counters[i] === undefined ? 1 : counters[i];
        line.firstChild.style.height = (c / 3 * 100) + '%';
        line.classList.toggle('weak', c < 2);
        line.title = 'entry ' + i + ': ' + ['strongly not taken', 'weakly not taken', 'weakly taken', 'strongly taken'][c];
      }
    }
    if (event) {
      el.eraEvent.textContent = event;
      restartAnimation(el.eraEvent, 'flash');
    }
  }

  function renderCacheLines(container, tags) {
    for (let i = 0; i < container.children.length; i++) {
      const tag = tags && tags[i] !== undefined ? tags[i] : -1;
      const line = container.children[i];
      line.classList.toggle('valid', tag >= 0);
      line.textContent = tag >= 0 ? hex2(tag) : '--';
      line.title = 'line ' + i + (tag >= 0 ? ': page 0x' + hex2(tag) : ': empty');
    }
  }

  function restartAnimation(element, className) {
    element.classList.remove(className);
    void element.offsetWidth;
    element.classList.add(className);
  }

  function setStatusText(kind, text) {
    ui.lastStatus = { kind, key: null, params: null, text };
    el.statusBadge.className = 'badge ' + kind;
    el.statusBadge.textContent = t('badge.' + kind);
    el.statusText.textContent = text;
  }

  function setStatusKey(kind, key, params) {
    ui.lastStatus = { kind, key, params: params || null, text: null };
    el.statusBadge.className = 'badge ' + kind;
    el.statusBadge.textContent = t('badge.' + kind);
    el.statusText.textContent = t(key, params);
  }

  function refreshStatus() {
    const s = ui.lastStatus;
    if (s.key) setStatusKey(s.kind, s.key, s.params); else setStatusText(s.kind, s.text);
  }

  function faultName(state) {
    return 'i18n:fault.' + state.fault;
  }

  function appendOutput(port, value) {
    const span = document.createElement('span');
    span.className = (port === 1 ? 'out-chr' : 'out-num') + ' pop';
    span.textContent = port === 1 ? String.fromCharCode(value) : (ui.outputCount > 0 ? ' ' : '') + value;
    if (port !== 1) ui.outputCount++;
    el.consoleText.appendChild(span);
  }

  function appendTrace(trace) {
    const row = document.createElement('div');
    row.className = 'trow ' + trace.kind + (trace.state.fault ? ' fault' : '');
    row.innerHTML = '<span class="taddr"></span><span class="tbytes"></span><span class="ttext"></span><span class="tcyc"></span>';
    row.children[0].textContent = hex4(trace.address);
    row.children[1].textContent = trace.bytes.map(hex2).join(' ');
    row.children[2].textContent = trace.state.fault ? trace.disassembly + '  ' + trace.state.fault : trace.disassembly;
    row.children[3].textContent = trace.cycles ? trace.cycles + ' ' + t('trace.cyc') : '';
    el.trace.appendChild(row);
    ui.traceRows++;
    while (ui.traceRows > 400) { el.trace.removeChild(el.trace.firstChild); ui.traceRows--; }
    el.trace.scrollTop = el.trace.scrollHeight;
  }

  // ------------------------------------------------------------ animation ----

  function centerOf(element) {
    const r = element.getBoundingClientRect();
    const s = el.stage.getBoundingClientRect();
    return { x: r.left + r.width / 2 - s.left, y: r.top + r.height / 2 - s.top };
  }

  /** Moves a glowing packet from one element to another across a bus lane. */
  function busTransfer(lane, fromEl, toEl, duration) {
    const laneEl = lane === 'addr' ? el.laneAddr : el.laneData;
    const color = lane === 'addr' ? 'var(--amber)' : 'var(--cyan)';
    const reverse = toEl.closest('.die') !== null;
    laneEl.classList.toggle('reverse', reverse);
    laneEl.classList.add('active');
    if (duration < 40) {
      setTimeout(() => laneEl.classList.remove('active'), 120);
      return Promise.resolve();
    }

    const svgNs = 'http://www.w3.org/2000/svg';
    const from = centerOf(fromEl);
    const via = centerOf(laneEl);
    const to = centerOf(toEl);
    const d = 'M' + from.x + ',' + from.y + ' Q' + (from.x + via.x) / 2 + ',' + from.y + ' ' + via.x + ',' + via.y +
      ' Q' + (via.x + to.x) / 2 + ',' + to.y + ' ' + to.x + ',' + to.y;
    const wire = document.createElementNS(svgNs, 'path');
    wire.setAttribute('d', d);
    wire.setAttribute('class', 'wire');
    wire.setAttribute('stroke', color);
    const packet = document.createElementNS(svgNs, 'circle');
    packet.setAttribute('r', '5');
    packet.setAttribute('fill', color);
    packet.setAttribute('class', 'packet');
    el.overlay.append(wire, packet);
    const length = wire.getTotalLength();

    return new Promise((resolve) => {
      const start = performance.now();
      function frame(now) {
        const t = Math.min(1, (now - start) / duration);
        const eased = t < .5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
        const p = wire.getPointAtLength(length * eased);
        packet.setAttribute('cx', p.x);
        packet.setAttribute('cy', p.y);
        if (t < 1) {
          requestAnimationFrame(frame);
        } else {
          packet.remove();
          laneEl.classList.remove('active');
          wire.style.transition = 'opacity .35s';
          wire.style.opacity = '0';
          setTimeout(() => wire.remove(), 350);
          resolve();
        }
      }
      requestAnimationFrame(frame);
    });
  }

  function flashEraFor(op) {
    if (!ui.timing) return;
    const name = op.name || '';
    if (name === 'queue hit') {
      const cell = el.queueCells.children[0];
      if (cell) restartAnimation(cell, 'hit');
    } else if (name.startsWith('I-cache') || name.startsWith('D-cache')) {
      const container = name.startsWith('I-cache') ? el.icache : el.dcache;
      const line = container.children[Math.floor(op.address / 16) % 16];
      if (line) restartAnimation(line, name.endsWith('hit') ? 'hit' : 'miss');
    }
  }

  async function animateOp(op, d) {
    switch (op.kind) {
      case 'fetch':
      case 'read': {
        if (op.kind === 'fetch') setPhase('fetch');
        const cell = cellFor(op.address);
        if (cell) {
          await busTransfer('addr', regEl('MAR'), cell, d * 1.3);
          restartAnimation(cell, 'flash-read');
          await busTransfer('data', cell, regEl('MDR'), d * 1.3);
        } else if (d) {
          await sleep(d * .6);
        }
        break;
      }
      case 'write': {
        const cell = cellFor(op.address);
        if (cell) {
          await busTransfer('addr', regEl('MAR'), cell, d * 1.2);
          await busTransfer('data', regEl('MDR'), cell, d * 1.3);
        }
        ui.memory[op.address] = op.value;
        renderCell(op.address);
        if (cell) restartAnimation(cell, 'flash-write');
        if (d) await sleep(d * .4);
        break;
      }
      case 'decode':
        setPhase('decode');
        el.decodedText.textContent = op.text;
        restartAnimation(el.decodedText, 'flash');
        if (d) await sleep(d * .9);
        break;
      case 'execute':
        setPhase('execute');
        if (d) await sleep(d * .5);
        break;
      case 'alu':
        el.aluA.textContent = op.before > 0xFF ? hex4(op.before) : hex2(op.before);
        el.aluB.textContent = op.after > 0xFF ? hex4(op.after) : hex2(op.after);
        el.aluOp.textContent = op.name;
        el.aluResult.textContent = op.value > 0xFF ? hex4(op.value) : hex2(op.value);
        restartAnimation(el.alu, 'active');
        if (d) await sleep(d * 1.6);
        break;
      case 'register': {
        const internal = op.name === 'MAR' || op.name === 'MDR' || op.name === 'IR';
        setRegister(op.name, op.after, d > 0);
        if (op.name === 'SP') { if (ui.state) { const before = ui.state.sp; ui.state = Object.assign({}, ui.state, { sp: op.after }); renderStack(before, op.after); } }
        if (d) await sleep(internal ? d * .2 : d * .6);
        break;
      }
      case 'flags':
        renderFlags(op.after, d > 0);
        if (d) await sleep(d * .5);
        break;
      case 'jump': {
        const row = ui.rowByAddress.get(op.address);
        if (op.value && row) restartAnimation(row, 'target');
        if (d) await sleep(d * .5);
        break;
      }
      case 'output':
        if (op.address === 0 || op.address === 1) appendOutput(op.address, op.value);
        else restartAnimation(el.timerLed.closest('.device'), 'flash');
        if (d) await sleep(d * .6);
        break;
      case 'input':
        if ((op.address === 0 || op.address === 1) && ui.inputQueue.length && op.value !== 0) ui.inputQueue = ui.inputQueue.slice(1);
        el.inputQueue.textContent = ui.inputQueue.length ? JSON.stringify(ui.inputQueue) : t('dev.empty');
        restartAnimation(el.inputText.closest('.device'), 'flash');
        if (d) await sleep(d * .6);
        break;
      case 'interrupt': {
        setPhase('interrupt');
        const led = $('irq-' + op.value);
        if (led) restartAnimation(led, 'flash');
        el.decodedText.textContent = op.text;
        restartAnimation(el.decodedText, 'flash');
        if (d) await sleep(d * 1.2);
        break;
      }
      case 'wait':
        setPhase('waiting');
        if (d) await sleep(d * .3);
        break;
      case 'timing':
        flashEraFor(op);
        el.eraEvent.textContent = I18N.eventText(op) + (op.value ? '  (+' + op.value + ')' : '');
        restartAnimation(el.eraEvent, 'flash');
        if (d && op.value) await sleep(d * .5);
        break;
      case 'halt':
        if (op.text) setPhase('waiting'); else el.die.classList.add('halted');
        break;
      case 'fault':
        el.die.classList.add('fault');
        break;
      default:
        break;
    }
  }

  /** Plays the whole trace of one step, then syncs every view with the final state. */
  async function playStep(trace) {
    const d = delay();
    highlightListing(trace.address, -1);
    markCurrentCells(trace.address, trace.bytes.length);
    restartAnimation(el.clockLed, 'on');
    for (const op of trace.ops) await animateOp(op, d);
    finishStep(trace);
  }

  function applyTraceQuietly(trace) {
    for (const op of trace.ops) {
      if (op.kind === 'write') { ui.memory[op.address] = op.value; renderCell(op.address); }
      else if (op.kind === 'output') { if (op.address === 0 || op.address === 1) appendOutput(op.address, op.value); }
      else if (op.kind === 'input') { if ((op.address === 0 || op.address === 1) && ui.inputQueue.length && op.value !== 0) ui.inputQueue = ui.inputQueue.slice(1); }
      else if (op.kind === 'halt') { if (!op.text) el.die.classList.add('halted'); }
      else if (op.kind === 'fault') el.die.classList.add('fault');
      else if (op.kind === 'alu') {
        el.aluA.textContent = op.before > 0xFF ? hex4(op.before) : hex2(op.before); el.aluB.textContent = op.after > 0xFF ? hex4(op.after) : hex2(op.after);
        el.aluOp.textContent = op.name; el.aluResult.textContent = op.value > 0xFF ? hex4(op.value) : hex2(op.value);
      } else if (op.kind === 'decode' || op.kind === 'interrupt') el.decodedText.textContent = op.text;
    }
  }

  function finishStep(trace) {
    const before = ui.state;
    ui.state = trace.state;
    renderRegisters(trace.state, false);
    renderFlags(trace.state.flags, false);
    renderDevices(trace.state, false);
    if (before) renderStack(before.sp, trace.state.sp);
    renderStats(trace.state, trace.cycles);
    renderEra(trace.timing, null);
    appendTrace(trace);
    highlightListing(trace.address, trace.state.halted ? -1 : trace.state.ip);
    el.clockLed.classList.remove('on');
    if (trace.state.fault) {
      setPhase('halted');
      setStatusKey('fault', 'status.fault', { fault: faultName(trace.state), message: trace.state.faultMessage || '' });
    } else if (trace.state.halted) {
      setPhase('halted');
      markCurrentCells(0, 0);
      setStatusKey('halted', 'status.halted', { n: trace.state.instructions, c: trace.state.cycles });
    } else if (trace.state.waiting) {
      setPhase('waiting');
    }
  }

  function syncAfterBatch(traces) {
    const last = traces[traces.length - 1];
    const before = ui.state;
    ui.state = last.state;
    renderRegisters(last.state, false);
    renderFlags(last.state.flags, false);
    renderDevices(last.state, false);
    if (before) renderStack(before.sp, last.state.sp);
    renderStats(last.state, last.cycles);
    renderEra(last.timing, null);
    highlightListing(last.address, last.state.halted ? -1 : last.state.ip);
    markCurrentCells(last.address, last.bytes.length);
    setPhase(last.state.phase);
    if (last.state.fault) setStatusKey('fault', 'status.fault', { fault: faultName(last.state), message: last.state.faultMessage || '' });
    else if (last.state.halted) { markCurrentCells(0, 0); setStatusKey('halted', 'status.halted', { n: last.state.instructions, c: last.state.cycles }); }
  }

  // -------------------------------------------------------------- control ----

  async function ensureProgram() {
    if (ui.program && !ui.modified) return true;
    return assemble();
  }

  function updateButtons() {
    el.btnRun.textContent = ui.running ? t('btn.pause') : t('btn.run');
    el.btnRun.classList.toggle('pause', ui.running);
    el.btnStep.disabled = ui.running;
    el.btnAssemble.disabled = ui.running;
    el.die.classList.toggle('running', ui.running);
  }

  async function stopRunning() {
    ui.running = false;
    if (ui.runPromise) await ui.runPromise;
  }

  async function runLoop() {
    let first = true;
    while (ui.running && !ui.engine.halted) {
      if (!first && !ui.engine.busy && ui.breakpoints.has(ui.engine.ip)) {
        setStatusKey('breakpoint', 'status.breakpoint', { ip: hex4(ui.engine.ip) });
        break;
      }
      first = false;
      if (delay() === 0) {
        let traces;
        try {
          traces = await ui.engine.batch(512, ui.breakpoints);
        } catch (error) {
          setStatusText('error', error.message);
          break;
        }
        for (const trace of traces) applyTraceQuietly(trace);
        if (traces.length) {
          for (const trace of traces.slice(-40)) appendTrace(trace);
          syncAfterBatch(traces);
        }
        await nextFrame();
      } else {
        let trace;
        try {
          trace = await ui.engine.step();
        } catch (error) {
          setStatusText('error', error.message);
          break;
        }
        await playStep(trace);
      }
    }
    ui.running = false;
    updateButtons();
    if (!ui.engine.halted && el.statusBadge.classList.contains('running')) {
      setStatusKey('paused', 'status.paused', { ip: hex4(ui.engine.ip) });
    }
  }

  async function toggleRun() {
    if (ui.running) { await stopRunning(); return; }
    if (ui.busy) return;
    if (!(await ensureProgram())) return;
    if (ui.engine.halted) await reloadProgram();
    ui.running = true;
    updateButtons();
    setStatusKey('running', 'status.running', { mode: delay() === 0 ? 'i18n:mode.full' : 'i18n:mode.animated', era: ERA_LABEL[ui.microarchitecture] });
    ui.runPromise = runLoop();
    await ui.runPromise;
    ui.runPromise = null;
  }

  async function stepOnce() {
    if (ui.running || ui.busy) return;
    if (!(await ensureProgram())) return;
    if (ui.engine.halted) {
      setStatusKey('halted', 'status.halted.reset');
      return;
    }
    ui.busy = true;
    el.btnStep.disabled = true;
    try {
      setStatusKey('running', 'status.stepping');
      const trace = await ui.engine.step();
      await playStep(trace);
      if (!trace.state.halted) setStatusKey('paused', 'status.next', { ip: hex4(trace.state.ip), wait: trace.state.waiting ? 'i18n:status.next.wait' : '' });
    } catch (error) {
      setStatusText('error', error.message);
    } finally {
      ui.busy = false;
      el.btnStep.disabled = false;
    }
  }

  async function resetMachine() {
    await stopRunning();
    if (!ui.program || ui.modified) {
      if (!(await assemble())) return;
    } else {
      try { await reloadProgram(); } catch (error) { setStatusText('error', error.message); return; }
    }
    setStatusKey('ready', 'status.reset');
    updateButtons();
  }

  async function selectEngine(id) {
    await stopRunning();
    ui.engine = ui.engines[id] || ui.engines.js;
    el.engineSelect.value = ui.engine.id;
    el.statEngine.textContent = engineName();
    try { localStorage.setItem('cisc16.engine', ui.engine.id); } catch (e) { /* storage unavailable */ }
    ui.modified = true;
    await assemble();
    updateButtons();
  }

  async function selectArchitecture(id) {
    await stopRunning();
    ui.microarchitecture = id;
    el.archSelect.value = id;
    try { localStorage.setItem('cisc16.arch', id); } catch (e) { /* storage unavailable */ }
    ui.modified = true;
    await assemble();
    updateButtons();
  }

  async function sendInput() {
    const text = el.inputText.value;
    if (!text) return;
    try {
      const queued = await ui.engine.sendInput(text);
      ui.inputQueue = queued;
      el.inputQueue.textContent = ui.inputQueue.length ? JSON.stringify(ui.inputQueue) : t('dev.empty');
      restartAnimation(el.inputText.closest('.device'), 'flash');
      setStatusKey(ui.lastStatus.kind, 'status.queued', { n: text.length });
    } catch (error) {
      setStatusText('error', error.message);
    }
  }

  /** Looks for the CISCProcessorWeb API next to the page; when it answers, the .NET engine becomes available. */
  async function detectServer() {
    if (location.protocol === 'file:' || location.hostname.endsWith('github.io')) return;
    const base = location.pathname.replace(/[^/]*$/, '');
    const info = await RemoteEngine.detect(base);
    if (!info) return;
    ui.engines.dotnet = new RemoteEngine(base, info);
    const option = document.createElement('option');
    option.value = 'dotnet';
    option.textContent = t('engine.dotnet');
    el.engineSelect.appendChild(option);
    el.engineField.hidden = false;
    let preferred = 'dotnet';
    try { preferred = localStorage.getItem('cisc16.engine') || preferred; } catch (e) { /* storage unavailable */ }
    if (preferred === 'dotnet') await selectEngine('dotnet');
  }

  async function loadSample(id) {
    const sample = SAMPLES.find((s) => s.id === id);
    if (!sample) return;
    el.source.value = sample.source;
    el.inputText.value = sample.input || '';
    ui.modified = true;
    ui.breakpoints.clear();
    await assemble();
  }

  function setSpeed(value) {
    ui.speed = Number(value);
    el.speedLabel.value = t('speed.' + ui.speed);
    try { localStorage.setItem('cisc16.speed', String(ui.speed)); } catch (e) { /* storage unavailable */ }
  }

  function applyTheme(theme) {
    document.documentElement.dataset.theme = theme;
    try { localStorage.setItem('cisc16.theme', theme); } catch (e) { /* storage unavailable */ }
  }

  // ------------------------------------------------------------- language ----

  function engineName() {
    return ui.engine.id === 'js' ? t('engine.name.js') : ui.engine.name;
  }

  function buildGuide() {
    el.guideNav.innerHTML = '';
    el.guideContent.innerHTML = '';
    I18N.guide().forEach((section, index) => {
      const id = 'guide-section-' + index;
      const link = document.createElement('a');
      link.href = '#' + id;
      link.textContent = section.h;
      link.addEventListener('click', (event) => { event.preventDefault(); $(id).scrollIntoView({ block: 'start' }); });
      el.guideNav.appendChild(link);
      const block = document.createElement('section');
      block.id = id;
      const heading = document.createElement('h3');
      heading.textContent = section.h;
      block.appendChild(heading);
      block.insertAdjacentHTML('beforeend', section.html);
      el.guideContent.appendChild(block);
    });
  }

  /** Switches every text on the page to `lang` and re-renders the parts built by script. */
  function applyLanguage(lang) {
    I18N.language = lang;
    lang = I18N.language;
    document.documentElement.lang = lang;
    document.title = t('app.title');
    for (const node of document.querySelectorAll('[data-i18n]')) node.textContent = t(node.dataset.i18n);
    for (const node of document.querySelectorAll('[data-i18n-title]')) node.title = t(node.dataset.i18nTitle);
    for (const node of document.querySelectorAll('[data-i18n-placeholder]')) node.placeholder = t(node.dataset.i18nPlaceholder);
    for (const node of document.querySelectorAll('[data-i18n-html]')) node.innerHTML = t(node.dataset.i18nHtml);
    for (const button of document.querySelectorAll('.lang-switch button')) button.classList.toggle('active', button.dataset.lang === lang);
    buildSampleSelect();
    buildReference();
    buildGuide();
    setSpeed(ui.speed);
    updateButtons();
    refreshStatus();
    el.statEngine.textContent = engineName();
    for (const option of el.engineSelect.options) option.textContent = t(option.value === 'js' ? 'engine.js' : 'engine.dotnet');
    if (ui.state) { renderStats(ui.state, Number(el.statLastCycles.textContent) || 0); renderDevices(ui.state, false); }
    if (ui.timing) renderEra(ui.timing, null);
    if (ui.modified) el.stats.textContent = t('stats.modified');
    else if (ui.program) el.stats.textContent = t('stats.program', { bytes: ui.program.length, entry: hex4(ui.program.entryPoint), symbols: Object.keys(ui.program.symbols).length });
    try { localStorage.setItem('cisc16.lang', lang); } catch (e) { /* storage unavailable */ }
  }

  // ------------------------------------------------------------------ init ----

  function init() {
    buildMemoryGrid();
    buildEraWidgets();
    ui.engines.js = ui.engine;
    const jsOption = document.createElement('option');
    jsOption.value = 'js';
    jsOption.textContent = t('engine.js');
    el.engineSelect.appendChild(jsOption);
    el.engineSelect.value = 'js';

    let theme = 'dark';
    let speed = 5;
    let arch = 'Classic1975';
    try {
      theme = localStorage.getItem('cisc16.theme') || theme;
      speed = Number(localStorage.getItem('cisc16.speed')) || speed;
      arch = localStorage.getItem('cisc16.arch') || arch;
    } catch (e) { /* storage unavailable */ }
    applyTheme(theme);
    el.speed.value = speed;
    const params = new URLSearchParams(location.search);
    let lang = (navigator.language || 'en').toLowerCase().startsWith('ru') ? 'ru' : 'en';
    try { lang = localStorage.getItem('cisc16.lang') || lang; } catch (e) { /* storage unavailable */ }
    if (I18N.languages.includes(params.get('lang'))) lang = params.get('lang');
    applyLanguage(lang);
    if (['Classic1975', 'Prefetch1985', 'Pipelined1995'].includes(params.get('era'))) arch = params.get('era');
    if (!Timing.MICROARCHITECTURES.some((m) => m.id === arch)) arch = 'Classic1975';
    ui.microarchitecture = arch;
    el.archSelect.value = arch;

    el.btnAssemble.addEventListener('click', async () => { await stopRunning(); await assemble(); updateButtons(); });
    el.engineSelect.addEventListener('change', () => selectEngine(el.engineSelect.value));
    el.archSelect.addEventListener('change', () => selectArchitecture(el.archSelect.value));
    el.btnRun.addEventListener('click', toggleRun);
    el.btnStep.addEventListener('click', stepOnce);
    el.btnReset.addEventListener('click', resetMachine);
    el.btnClearTrace.addEventListener('click', () => { el.trace.innerHTML = ''; ui.traceRows = 0; });
    el.btnReference.addEventListener('click', () => el.reference.showModal());
    el.btnCloseReference.addEventListener('click', () => el.reference.close());
    el.btnGuide.addEventListener('click', () => { el.guide.showModal(); el.guideContent.scrollTop = 0; });
    el.btnCloseGuide.addEventListener('click', () => el.guide.close());
    for (const button of document.querySelectorAll('.lang-switch button')) button.addEventListener('click', () => applyLanguage(button.dataset.lang));
    el.btnTheme.addEventListener('click', () => applyTheme(document.documentElement.dataset.theme === 'light' ? 'dark' : 'light'));
    el.speed.addEventListener('input', () => setSpeed(el.speed.value));
    el.btnSendInput.addEventListener('click', sendInput);
    el.inputText.addEventListener('keydown', (event) => { if (event.key === 'Enter') { event.preventDefault(); sendInput(); } });
    el.btnPagePrev.addEventListener('click', () => showPage(ui.page - 1, true));
    el.btnPageNext.addEventListener('click', () => showPage(ui.page + 1, true));
    el.btnPageIp.addEventListener('click', () => { if (ui.state) showPage(pageOf(ui.state.ip), true); });
    el.btnPageSp.addEventListener('click', () => { if (ui.state) showPage(pageOf(ui.state.sp), true); });
    el.followBus.addEventListener('change', () => { ui.follow = el.followBus.checked; });
    el.sampleSelect.addEventListener('change', async () => {
      await stopRunning();
      if (el.sampleSelect.value) await loadSample(el.sampleSelect.value);
      updateButtons();
    });
    el.source.addEventListener('input', () => {
      ui.modified = true;
      el.sampleSelect.value = '';
      el.stats.textContent = t('stats.modified');
      if (!el.error.hidden) clearError();
      else updateGutter();
    });
    el.source.addEventListener('scroll', () => { el.gutter.scrollTop = el.source.scrollTop; });
    el.source.addEventListener('keydown', (event) => {
      if (event.key === 'Tab') {
        event.preventDefault();
        const start = el.source.selectionStart;
        el.source.setRangeText('        ', start, el.source.selectionEnd, 'end');
        el.source.dispatchEvent(new Event('input'));
      }
    });

    document.addEventListener('keydown', (event) => {
      if (event.target === el.inputText) return;
      if (event.key === 'F5') { event.preventDefault(); toggleRun(); }
      else if (event.key === 'F8') { event.preventDefault(); stepOnce(); }
      else if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); stopRunning().then(assemble).then(updateButtons); }
      else if (event.key === 'Escape' && ui.running) { stopRunning(); }
    });

    const initial = SAMPLES.some((s) => s.id === params.get('program')) ? params.get('program') : SAMPLES[0].id;
    el.sampleSelect.value = initial;
    loadSample(initial).then(updateButtons).then(detectServer);
  }

  document.addEventListener('DOMContentLoaded', init);
})();
