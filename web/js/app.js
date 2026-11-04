/*
 * CISC-8 Processor Visualizer - user interface and animation player.
 * The player consumes the micro-operation trace produced by CPU.step() (identical in shape to the
 * StepTrace of the .NET library) and turns every bus transfer, ALU operation and register write
 * into an animation. The engine behind the trace is swappable (see LocalEngine).
 */
(function () {
  'use strict';

  const { ISA, CPU, Assembler, SAMPLES } = window.CISC;
  const $ = (id) => document.getElementById(id);
  const hex2 = ISA.hex2;
  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
  const nextFrame = () => new Promise((resolve) => requestAnimationFrame(() => resolve()));

  // ---------------------------------------------------------------- engine ----

  /** Runs the program on the JavaScript mirror of the processor, in the browser. */
  class LocalEngine {
    constructor() {
      this.cpu = new CPU();
      this.id = 'js';
      this.name = 'JavaScript';
    }

    /** Assembles the source and loads it. Throws an AssemblyError (with .line) when the source is invalid. */
    async load(source) {
      const program = Assembler.assemble(source);
      this.cpu.loadProgram(program);
      return { program, state: this.cpu.getState() };
    }

    get halted() { return this.cpu.halted; }

    get ip() { return this.cpu.ip; }

    async step() { return this.cpu.step(); }

    /** Runs up to `count` instructions without animation, stopping in front of any address in `stopAt`. */
    async batch(count, stopAt) {
      const traces = [];
      while (traces.length < count && !this.cpu.halted) {
        if (traces.length > 0 && stopAt.has(this.cpu.ip)) break;
        traces.push(this.cpu.step());
      }
      return traces;
    }
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
        response = await fetch(this.base + path, {
          method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body || {}),
        });
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

    async load(source) {
      const result = await this.request('api/sessions', { source });
      this.session = result.id;
      this.track(result.state);
      return { program: result.program, state: result.state };
    }

    track(state) {
      this._halted = state.halted;
      this._ip = state.ip;
    }

    get halted() { return this._halted; }

    get ip() { return this._ip; }

    async step() {
      const result = await this.request('api/sessions/' + this.session + '/step');
      this.track(result.trace.state);
      return result.trace;
    }

    async batch(count, stopAt) {
      const result = await this.request('api/sessions/' + this.session + '/run', { maxInstructions: count, stopAt: Array.from(stopAt) });
      if (result.traces.length) this.track(result.traces[result.traces.length - 1].state);
      return result.traces;
    }
  }

  // ----------------------------------------------------------------- state ----

  const DELAYS = [0, 520, 340, 220, 140, 90, 55, 30, 14, 5, 0];
  const SPEED_LABELS = ['', 'crawl', 'very slow', 'slow', 'relaxed', 'normal', 'brisk', 'fast', 'faster', 'blazing', 'turbo'];

  const ui = {
    engine: new LocalEngine(),
    engines: {},
    program: null,
    modified: true,
    running: false,
    busy: false,
    runPromise: null,
    speed: 5,
    breakpoints: new Set(),
    memory: new Uint8Array(256),
    cellKind: new Array(256).fill(''),
    state: null,
    traceRows: 0,
    outputCount: 0,
    currentRow: null,
    nextRow: null,
    currentCells: [],
    lastSp: 0xFF,
  };

  const el = {
    source: $('source'), gutter: $('gutter'), error: $('assemble-error'), stats: $('program-stats'),
    listing: $('listing'), sampleSelect: $('sample-select'), speed: $('speed'), speedLabel: $('speed-label'),
    btnAssemble: $('btn-assemble'), btnRun: $('btn-run'), btnStep: $('btn-step'), btnReset: $('btn-reset'),
    btnReference: $('btn-reference'), btnCloseReference: $('btn-close-reference'), btnTheme: $('btn-theme'),
    btnClearTrace: $('btn-clear-trace'),
    stage: $('stage'), die: $('die'), overlay: $('overlay'), memory: $('memory'),
    decodedText: $('decoded-text'), clockLed: $('clock-led'),
    statCycles: $('stat-cycles'), statInstructions: $('stat-instructions'), statIp: $('stat-ip'), statSp: $('stat-sp'),
    statPhase: $('stat-phase'), statEngine: $('stat-engine'),
    alu: $('alu'), aluA: $('alu-a').querySelector('.port-value'), aluB: $('alu-b').querySelector('.port-value'),
    aluOp: $('alu-op'), aluResult: $('alu-result'),
    laneAddr: $('lane-addr'), laneData: $('lane-data'),
    consoleText: $('console-text'), trace: $('trace'),
    statusBadge: $('status-badge'), statusText: $('status-text'),
    reference: $('reference'), referenceTable: $('reference-table'),
    engineField: $('engine-field'), engineSelect: $('engine-select'),
  };

  const regEl = (name) => $('reg-' + name);
  const cellEl = (address) => $('cell-' + (address & 0xFF));
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
      h.textContent = hex2(row * 16);
      frag.appendChild(h);
      for (let col = 0; col < 16; col++) {
        const address = row * 16 + col;
        const cell = document.createElement('div');
        cell.className = 'cell';
        cell.id = 'cell-' + address;
        cell.textContent = '00';
        cell.title = cellTitle(address, 0);
        frag.appendChild(cell);
      }
    }
    el.memory.appendChild(frag);
  }

  function cellTitle(address, value) {
    const chr = value >= 32 && value < 127 ? " '" + String.fromCharCode(value) + "'" : '';
    return '[0x' + hex2(address) + '] = 0x' + hex2(value) + ' = ' + value + chr;
  }

  function buildSampleSelect() {
    for (const sample of SAMPLES) {
      const option = document.createElement('option');
      option.value = sample.id;
      option.textContent = sample.name;
      option.title = sample.description;
      el.sampleSelect.appendChild(option);
    }
    const custom = document.createElement('option');
    custom.value = '';
    custom.textContent = 'Custom program';
    el.sampleSelect.appendChild(custom);
  }

  function buildReference() {
    const families = [
      ['Control', (i) => i.opcode < 0x10], ['Data movement', (i) => i.opcode >= 0x10 && i.opcode < 0x20],
      ['Stack', (i) => i.opcode >= 0x20 && i.opcode < 0x30], ['Arithmetic', (i) => i.opcode >= 0x30 && i.opcode < 0x50],
      ['Logic and shifts', (i) => i.opcode >= 0x50 && i.opcode < 0x60], ['Control flow', (i) => i.opcode >= 0x60 && i.opcode < 0x70],
      ['Flags', (i) => i.opcode >= 0x70 && i.opcode < 0x80], ['Input / output', (i) => i.opcode >= 0xF0],
    ];
    const table = document.createElement('table');
    table.innerHTML = '<thead><tr><th>Op</th><th>Syntax</th><th>Bytes</th><th>Cycles</th><th>Description</th></tr></thead>';
    const body = document.createElement('tbody');
    for (const [name, filter] of families) {
      const head = document.createElement('tr');
      head.className = 'family';
      head.innerHTML = '<td colspan="5"></td>';
      head.firstChild.textContent = name;
      body.appendChild(head);
      for (const i of ISA.all.filter(filter)) {
        const row = document.createElement('tr');
        row.innerHTML = '<td class="op"></td><td class="syn"></td><td class="num"></td><td class="num"></td><td></td>';
        row.children[0].textContent = hex2(i.opcode);
        row.children[1].textContent = i.syntax.template;
        row.children[2].textContent = i.length;
        row.children[3].textContent = i.cycles + (i.family === 'jump' || i.family === 'loop' ? ' (+1 taken)' : '');
        row.children[4].textContent = i.description;
        body.appendChild(row);
      }
    }
    table.appendChild(body);
    el.referenceTable.appendChild(table);
  }

  // --------------------------------------------------------------- editor ----

  function updateGutter(errorLine) {
    const lines = el.source.value.split('\n').length;
    let text = '';
    for (let i = 1; i <= lines; i++) {
      text += (i === errorLine ? '<span class="err">' + i + '</span>' : i) + '\n';
    }
    el.gutter.innerHTML = text;
    el.gutter.scrollTop = el.source.scrollTop;
  }

  function showError(error) {
    el.error.hidden = false;
    el.error.textContent = error.message;
    updateGutter(error.line);
    setStatus('error', error.message);
  }

  function clearError() {
    el.error.hidden = true;
    el.error.textContent = '';
    updateGutter();
  }

  async function assemble() {
    clearError();
    ui.busy = true;
    try {
      const { program, state } = await ui.engine.load(el.source.value);
      ui.program = program;
      ui.modified = false;
      el.stats.textContent = program.length + ' bytes, entry 0x' + hex2(program.entryPoint) + ', ' + Object.keys(program.symbols).length + ' symbols';
      renderListing(program);
      loadMemoryImage(program);
      resetMachineView(state);
      setStatus('ready', 'Program assembled and loaded. Press Run or Step.');
      return true;
    } catch (error) {
      ui.program = null;
      if (error.line) showError(error);
      else setStatus('error', error.message);
      return false;
    } finally {
      ui.busy = false;
    }
  }

  /** Reloads the current (already assembled) program into the engine, e.g. after HLT or Reset. */
  async function reloadProgram() {
    const { program, state } = await ui.engine.load(el.source.value);
    ui.program = program;
    loadMemoryImage(program);
    resetMachineView(state);
  }

  function loadMemoryImage(program) {
    ui.memory.fill(0);
    ui.cellKind.fill('');
    for (const line of program.listing) {
      for (let i = 0; i < line.bytes.length; i++) {
        const address = (line.address + i) & 0xFF;
        ui.memory[address] = line.bytes[i];
        ui.cellKind[address] = line.isInstruction ? 'code' : 'data';
      }
    }
  }

  function resetMachineView(state) {
    ui.state = state;
    ui.lastSp = 0xFF;
    for (let address = 0; address < 256; address++) renderCell(address);
    clearCurrentCells();
    renderStack(state.sp, true);
    renderRegisters(state, false);
    renderFlags(state.flags, false);
    setPhase('idle');
    el.decodedText.textContent = '—';
    el.aluA.textContent = '--';
    el.aluB.textContent = '--';
    el.aluOp.textContent = 'idle';
    el.aluResult.textContent = '--';
    el.die.classList.remove('halted', 'fault', 'running');
    el.consoleText.textContent = '';
    ui.outputCount = 0;
    el.trace.innerHTML = '';
    ui.traceRows = 0;
    highlightListing(-1, state.ip);
    renderStats(state);
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
      addr.textContent = line.bytes.length ? hex2(line.address) : '';
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
        addr.title = 'Toggle breakpoint at 0x' + hex2(line.address);
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
    if (ui.breakpoints.has(address)) {
      ui.breakpoints.delete(address);
      row.classList.remove('bp');
    } else {
      ui.breakpoints.add(address);
      row.classList.add('bp');
    }
  }

  function highlightListing(currentAddress, nextAddress) {
    if (ui.currentRow) ui.currentRow.classList.remove('current');
    if (ui.nextRow) ui.nextRow.classList.remove('next');
    ui.currentRow = ui.rowByAddress ? ui.rowByAddress.get(currentAddress) : null;
    ui.nextRow = ui.rowByAddress ? ui.rowByAddress.get(nextAddress) : null;
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

  function renderCell(address) {
    const cell = cellEl(address);
    const value = ui.memory[address];
    cell.textContent = hex2(value);
    cell.title = cellTitle(address, value);
    cell.classList.toggle('nonzero', value !== 0);
    cell.classList.toggle('code', ui.cellKind[address] === 'code');
    cell.classList.toggle('data', ui.cellKind[address] === 'data');
  }

  function renderStack(sp, force) {
    if (!force && sp === ui.lastSp) return;
    const from = Math.min(sp, ui.lastSp);
    for (let address = from; address < 256; address++) {
      const cell = cellEl(address);
      cell.classList.toggle('stack', address > sp);
      cell.classList.toggle('sp', address === sp);
    }
    ui.lastSp = sp;
  }

  function markCurrentCells(address, length) {
    clearCurrentCells();
    for (let i = 0; i < length; i++) {
      const cell = cellEl(address + i);
      cell.classList.add('current');
      ui.currentCells.push(cell);
    }
  }

  function clearCurrentCells() {
    for (const cell of ui.currentCells) cell.classList.remove('current');
    ui.currentCells = [];
  }

  function setRegister(name, value, flash) {
    const reg = regEl(name);
    if (!reg) return;
    const valueEl = reg.querySelector('.reg-value');
    valueEl.textContent = hex2(value);
    reg.title = name + ' = 0x' + hex2(value) + ' = ' + value;
    if (flash) restartAnimation(reg, 'flash');
  }

  function renderRegisters(state, flash) {
    setRegister('A', state.a, flash);
    for (let i = 0; i < 8; i++) setRegister('R' + i, state.r[i], flash);
    setRegister('IP', state.ip, flash);
    setRegister('SP', state.sp, flash);
    setRegister('IR', state.ir, flash);
    setRegister('MAR', state.mar, flash);
    setRegister('MDR', state.mdr, flash);
  }

  function renderFlags(flags, animate) {
    const names = { z: 'Zero', c: 'Carry', n: 'Negative', v: 'Overflow' };
    for (const name of ['z', 'c', 'n', 'v']) {
      const flag = $('flag-' + name);
      const on = (flags & CPU.FLAG[name]) !== 0;
      const was = flag.classList.contains('on');
      flag.classList.toggle('on', on);
      flag.title = names[name] + ' = ' + (on ? 1 : 0);
      if (animate && was !== on) restartAnimation(flag, 'changed');
    }
  }

  function renderStats(state) {
    el.statCycles.textContent = state.cycles;
    el.statInstructions.textContent = state.instructions;
    el.statIp.textContent = hex2(state.ip);
    el.statSp.textContent = hex2(state.sp);
    el.statPhase.textContent = state.phase;
  }

  function setPhase(phase) {
    const order = ['fetch', 'decode', 'execute'];
    const index = order.indexOf(phase);
    for (let i = 0; i < order.length; i++) {
      const p = $('phase-' + order[i]);
      p.classList.toggle('active', i === index);
      p.classList.toggle('done', index >= 0 && i < index);
    }
    el.statPhase.textContent = phase;
  }

  function restartAnimation(element, className) {
    element.classList.remove(className);
    void element.offsetWidth;
    element.classList.add(className);
  }

  function setStatus(kind, text) {
    el.statusBadge.className = 'badge ' + kind;
    el.statusBadge.textContent = kind === 'breakpoint' ? 'Breakpoint' : kind.charAt(0).toUpperCase() + kind.slice(1);
    el.statusText.textContent = text;
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
    row.className = 'trow' + (trace.state.fault ? ' fault' : '');
    row.innerHTML = '<span class="taddr"></span><span class="tbytes"></span><span class="ttext"></span><span class="tcyc"></span>';
    row.children[0].textContent = hex2(trace.address);
    row.children[1].textContent = trace.bytes.map(hex2).join(' ');
    row.children[2].textContent = trace.state.fault ? trace.disassembly + '  ' + trace.state.fault : trace.disassembly;
    row.children[3].textContent = trace.cycles ? trace.cycles + ' cyc' : '';
    el.trace.appendChild(row);
    ui.traceRows++;
    while (ui.traceRows > 400) {
      el.trace.removeChild(el.trace.firstChild);
      ui.traceRows--;
    }
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

  async function animateOp(op, d) {
    switch (op.kind) {
      case 'fetch':
      case 'read': {
        if (op.kind === 'fetch') setPhase('fetch');
        const cell = cellEl(op.address);
        await busTransfer('addr', regEl('MAR'), cell, d * 1.3);
        restartAnimation(cell, 'flash-read');
        await busTransfer('data', cell, regEl('MDR'), d * 1.3);
        break;
      }
      case 'write': {
        const cell = cellEl(op.address);
        await busTransfer('addr', regEl('MAR'), cell, d * 1.2);
        await busTransfer('data', regEl('MDR'), cell, d * 1.3);
        ui.memory[op.address] = op.value;
        renderCell(op.address);
        restartAnimation(cell, 'flash-write');
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
        el.aluA.textContent = hex2(op.before);
        el.aluB.textContent = hex2(op.after);
        el.aluOp.textContent = op.name;
        el.aluResult.textContent = hex2(op.value);
        restartAnimation(el.alu, 'active');
        if (d) await sleep(d * 1.6);
        break;
      case 'register': {
        const internal = op.name === 'MAR' || op.name === 'MDR' || op.name === 'IR';
        setRegister(op.name, op.after, d > 0);
        if (op.name === 'SP') renderStack(op.after, false);
        if (d) await sleep(internal ? d * .2 : d * .6);
        break;
      }
      case 'flags':
        renderFlags(op.after, d > 0);
        if (d) await sleep(d * .5);
        break;
      case 'jump': {
        const row = ui.rowByAddress && ui.rowByAddress.get(op.address);
        if (op.value && row) restartAnimation(row, 'target');
        if (d) await sleep(d * .5);
        break;
      }
      case 'output':
        appendOutput(op.address, op.value);
        if (d) await sleep(d * .6);
        break;
      case 'halt':
        el.die.classList.add('halted');
        break;
      case 'fault':
        el.die.classList.add('fault');
        break;
      default:
        break;
    }
  }

  /** Plays the whole trace of one instruction, then syncs every view with the final state. */
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
      else if (op.kind === 'output') appendOutput(op.address, op.value);
      else if (op.kind === 'halt') el.die.classList.add('halted');
      else if (op.kind === 'fault') el.die.classList.add('fault');
      else if (op.kind === 'alu') {
        el.aluA.textContent = hex2(op.before); el.aluB.textContent = hex2(op.after);
        el.aluOp.textContent = op.name; el.aluResult.textContent = hex2(op.value);
      } else if (op.kind === 'decode') el.decodedText.textContent = op.text;
    }
  }

  function finishStep(trace) {
    ui.state = trace.state;
    renderRegisters(trace.state, false);
    renderFlags(trace.state.flags, false);
    renderStack(trace.state.sp, false);
    renderStats(trace.state);
    appendTrace(trace);
    highlightListing(trace.address, trace.state.halted ? -1 : trace.state.ip);
    el.clockLed.classList.remove('on');
    if (trace.state.fault) {
      setPhase('halted');
      setStatus('fault', trace.state.fault + ': ' + (trace.state.faultMessage || '') + ' - reset to try again.');
    } else if (trace.state.halted) {
      setPhase('halted');
      clearCurrentCells();
      setStatus('halted', 'Halted after ' + trace.state.instructions + ' instructions and ' + trace.state.cycles + ' clock cycles.');
    }
  }

  // -------------------------------------------------------------- control ----

  async function ensureProgram() {
    if (ui.program && !ui.modified) return true;
    return assemble();
  }

  function updateButtons() {
    el.btnRun.textContent = ui.running ? 'Pause' : 'Run';
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
      if (!first && ui.breakpoints.has(ui.engine.ip)) {
        setStatus('breakpoint', 'Stopped at breakpoint 0x' + hex2(ui.engine.ip) + '. Press Run to continue or Step.');
        break;
      }
      first = false;
      if (delay() === 0) {
        let traces;
        try {
          traces = await ui.engine.batch(512, ui.breakpoints);
        } catch (error) {
          setStatus('error', error.message);
          break;
        }
        for (const trace of traces) applyTraceQuietly(trace);
        if (traces.length) {
          const last = traces[traces.length - 1];
          for (const trace of traces.slice(-40)) appendTrace(trace);
          ui.state = last.state;
          renderRegisters(last.state, false);
          renderFlags(last.state.flags, false);
          renderStack(last.state.sp, false);
          renderStats(last.state);
          highlightListing(last.address, last.state.halted ? -1 : last.state.ip);
          markCurrentCells(last.address, last.bytes.length);
          setPhase(last.state.phase);
          if (last.state.fault) setStatus('fault', last.state.fault + ': ' + (last.state.faultMessage || '') + ' - reset to try again.');
          else if (last.state.halted) { clearCurrentCells(); setStatus('halted', 'Halted after ' + last.state.instructions + ' instructions and ' + last.state.cycles + ' clock cycles.'); }
        }
        await nextFrame();
      } else {
        let trace;
        try {
          trace = await ui.engine.step();
        } catch (error) {
          setStatus('error', error.message);
          break;
        }
        await playStep(trace);
      }
    }
    ui.running = false;
    updateButtons();
    if (!ui.engine.halted && el.statusBadge.classList.contains('running')) {
      setStatus('paused', 'Paused at 0x' + hex2(ui.engine.ip) + '. Press Run to continue or Step.');
    }
  }

  async function toggleRun() {
    if (ui.running) {
      await stopRunning();
      return;
    }
    if (ui.busy) return;
    if (!(await ensureProgram())) return;
    if (ui.engine.halted) await reloadProgram();
    ui.running = true;
    updateButtons();
    setStatus('running', 'Running ' + (delay() === 0 ? 'at full speed' : 'with animation') + '. Press Pause (F5) to stop.');
    ui.runPromise = runLoop();
    await ui.runPromise;
    ui.runPromise = null;
  }

  async function stepOnce() {
    if (ui.running || ui.busy) return;
    if (!(await ensureProgram())) return;
    if (ui.engine.halted) {
      setStatus('halted', 'The processor is halted. Press Reset to run the program again.');
      return;
    }
    ui.busy = true;
    el.btnStep.disabled = true;
    try {
      setStatus('running', 'Executing one instruction.');
      const trace = await ui.engine.step();
      await playStep(trace);
      if (!trace.state.halted) setStatus('paused', 'Next instruction at 0x' + hex2(trace.state.ip) + '.');
    } catch (error) {
      setStatus('error', error.message);
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
      try {
        await reloadProgram();
      } catch (error) {
        setStatus('error', error.message);
        return;
      }
    }
    setStatus('ready', 'Processor reset. Press Run or Step.');
    updateButtons();
  }

  async function selectEngine(id) {
    await stopRunning();
    ui.engine = ui.engines[id] || ui.engines.js;
    el.engineSelect.value = ui.engine.id;
    el.statEngine.textContent = ui.engine.name;
    try { localStorage.setItem('cisc8.engine', ui.engine.id); } catch (e) { /* storage unavailable */ }
    ui.modified = true;
    await assemble();
    updateButtons();
  }

  /** Looks for the CISCProcessorWeb API next to the page; when it answers, the .NET engine becomes available. */
  async function detectServer() {
    if (location.protocol === 'file:') return;
    const base = location.pathname.replace(/[^/]*$/, '');
    const info = await RemoteEngine.detect(base);
    if (!info) return;
    ui.engines.dotnet = new RemoteEngine(base, info);
    const option = document.createElement('option');
    option.value = 'dotnet';
    option.textContent = '.NET server';
    el.engineSelect.appendChild(option);
    el.engineField.hidden = false;
    let preferred = 'dotnet';
    try { preferred = localStorage.getItem('cisc8.engine') || preferred; } catch (e) { /* storage unavailable */ }
    if (preferred === 'dotnet') await selectEngine('dotnet');
  }

  async function loadSample(id) {
    const sample = SAMPLES.find((s) => s.id === id);
    if (!sample) return;
    el.source.value = sample.source;
    ui.modified = true;
    ui.breakpoints.clear();
    await assemble();
  }

  function setSpeed(value) {
    ui.speed = Number(value);
    el.speedLabel.value = SPEED_LABELS[ui.speed];
    try { localStorage.setItem('cisc8.speed', String(ui.speed)); } catch (e) { /* storage unavailable */ }
  }

  function applyTheme(theme) {
    document.documentElement.dataset.theme = theme;
    try { localStorage.setItem('cisc8.theme', theme); } catch (e) { /* storage unavailable */ }
  }

  // ------------------------------------------------------------------ init ----

  function init() {
    buildMemoryGrid();
    buildSampleSelect();
    buildReference();
    ui.engines.js = ui.engine;
    const jsOption = document.createElement('option');
    jsOption.value = 'js';
    jsOption.textContent = 'JavaScript (browser)';
    el.engineSelect.appendChild(jsOption);
    el.engineSelect.value = 'js';
    el.statEngine.textContent = ui.engine.name;

    let theme = 'dark';
    let speed = 5;
    try {
      theme = localStorage.getItem('cisc8.theme') || theme;
      speed = Number(localStorage.getItem('cisc8.speed')) || speed;
    } catch (e) { /* storage unavailable */ }
    applyTheme(theme);
    el.speed.value = speed;
    setSpeed(speed);

    el.btnAssemble.addEventListener('click', async () => { await stopRunning(); await assemble(); updateButtons(); });
    el.engineSelect.addEventListener('change', () => selectEngine(el.engineSelect.value));
    el.btnRun.addEventListener('click', toggleRun);
    el.btnStep.addEventListener('click', stepOnce);
    el.btnReset.addEventListener('click', resetMachine);
    el.btnClearTrace.addEventListener('click', () => { el.trace.innerHTML = ''; ui.traceRows = 0; });
    el.btnReference.addEventListener('click', () => el.reference.showModal());
    el.btnCloseReference.addEventListener('click', () => el.reference.close());
    el.btnTheme.addEventListener('click', () => applyTheme(document.documentElement.dataset.theme === 'light' ? 'dark' : 'light'));
    el.speed.addEventListener('input', () => setSpeed(el.speed.value));
    el.sampleSelect.addEventListener('change', async () => {
      await stopRunning();
      if (el.sampleSelect.value) await loadSample(el.sampleSelect.value);
      updateButtons();
    });
    el.source.addEventListener('input', () => {
      ui.modified = true;
      el.sampleSelect.value = '';
      el.stats.textContent = 'modified - assemble to load';
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
      if (event.key === 'F5') { event.preventDefault(); toggleRun(); }
      else if (event.key === 'F8') { event.preventDefault(); stepOnce(); }
      else if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); stopRunning().then(assemble).then(updateButtons); }
      else if (event.key === 'Escape' && ui.running) { stopRunning(); }
    });

    const params = new URLSearchParams(location.search);
    const initial = SAMPLES.some((s) => s.id === params.get('program')) ? params.get('program') : SAMPLES[0].id;
    el.sampleSelect.value = initial;
    loadSample(initial).then(updateButtons).then(detectServer);
  }

  document.addEventListener('DOMContentLoaded', init);
})();
