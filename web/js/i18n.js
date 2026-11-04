/*
 * User interface strings and the guide, in English and Russian.
 * Engine messages (assembler errors, fault texts, timing event names) stay in English; the page
 * translates what it renders itself and maps the engine's event and statistic names where it can.
 */
(function (global) {
  'use strict';

  const en = {
    'app.title': 'CISC-16 Processor Visualizer',
    'brand.sub': 'Processor Visualizer',
    'toolbar.program': 'Program',
    'toolbar.era': 'Era',
    'toolbar.speed': 'Speed',
    'toolbar.engine': 'Engine',
    'era.Classic1975': '1975 multi-cycle',
    'era.Prefetch1985': '1985 prefetch queue',
    'era.Pipelined1995': '1995 pipeline + caches',
    'era.name.Classic1975': '1975 multi-cycle (8080 style)',
    'era.name.Prefetch1985': '1985 prefetch queue (8086 style)',
    'era.name.Pipelined1995': '1995 pipeline + caches (Pentium style)',
    'era.classic.text': 'Multi-cycle control unit: every bus cycle costs T-states and nothing overlaps. The row below counts the bus cycles.',
    'era.queue': 'Prefetch queue',
    'era.icache': 'I-cache',
    'era.dcache': 'D-cache',
    'era.predictor': 'Branch predictor',
    'era.queue.empty': '(empty)',
    'era.title': 'Micro-architecture',
    'btn.assemble': 'Assemble',
    'btn.run': 'Run',
    'btn.pause': 'Pause',
    'btn.step': 'Step',
    'btn.reset': 'Reset',
    'btn.reference': 'Instruction set',
    'btn.guide': 'Guide',
    'btn.theme': 'Theme',
    'btn.send': 'Send',
    'btn.clear': 'clear',
    'btn.close': 'Close',
    'tip.assemble': 'Assemble the source and load it into memory (Ctrl+Enter)',
    'tip.run': 'Run / pause (F5)',
    'tip.step': 'Execute one step (F8)',
    'tip.reset': 'Reset the processor and reload the program',
    'tip.sample': 'Load a sample program',
    'tip.era': 'Which generation of micro-architecture times the program',
    'tip.engine': 'Where the program runs: in the browser or on the .NET server',
    'tip.theme': 'Toggle light / dark theme',
    'tip.guide': 'How the stand works and how to write programs',
    'tip.reference': 'Instruction set reference',
    'tip.pagePrev': 'Previous page',
    'tip.pageNext': 'Next page',
    'tip.pageIp': 'Show the page IP points to',
    'tip.pageSp': 'Show the page SP points to',
    'tip.follow': 'Switch the page to every address the processor touches',
    'engine.js': 'JavaScript (browser)',
    'engine.dotnet': '.NET server',
    'engine.name.js': 'JavaScript',
    'sample.custom': 'Custom program',
    'speed.1': 'crawl', 'speed.2': 'very slow', 'speed.3': 'slow', 'speed.4': 'relaxed', 'speed.5': 'normal',
    'speed.6': 'brisk', 'speed.7': 'fast', 'speed.8': 'faster', 'speed.9': 'blazing', 'speed.10': 'turbo',
    'panel.source': 'Source',
    'panel.listing': 'Listing',
    'listing.hint': 'click an address to toggle a breakpoint',
    'panel.devices': 'Devices',
    'devices.hint': 'interrupts, timer, input',
    'dev.irq': 'Interrupt lines',
    'dev.iflag': 'I flag',
    'dev.enabled': 'enabled',
    'dev.timer': 'Timer (IRQ 0)',
    'dev.period': 'period',
    'dev.cycles': 'cycles',
    'dev.off': 'off',
    'dev.running': 'running',
    'dev.ticks': 'ticks',
    'dev.input': 'Input device (ports 1, 6)',
    'dev.input.placeholder': 'text for IN A, 1',
    'dev.queue': 'queue',
    'dev.empty': '(empty)',
    'die.label': 'CISC-16 · 8 bit data path · 16 bit addresses',
    'cu.title': 'Control unit',
    'phase.fetch': 'Fetch',
    'phase.decode': 'Decode',
    'phase.execute': 'Execute',
    'phase.irq': 'IRQ',
    'phase.wait': 'Wait',
    'clock': 'clock',
    'clock.cycles': 'cycles',
    'clock.instructions': 'instructions',
    'clock.last': 'last step',
    'regs.title': 'Register file',
    'alu.title': 'ALU',
    'alu.result': 'result',
    'alu.idle': 'idle',
    'bus.addr': 'address bus 16',
    'bus.data': 'data bus 8',
    'mem.page': 'page',
    'mem.follow': 'follow',
    'legend.code': 'code', 'legend.data': 'data', 'legend.stack': 'stack', 'legend.vectors': 'vectors', 'legend.ip': 'IP',
    'panel.output': 'Output ports',
    'output.hint': 'port 0 = numbers, port 1 = characters',
    'panel.trace': 'Execution trace',
    'trace.cyc': 'cyc',
    'status.ip': 'IP', 'status.sp': 'SP', 'status.phase': 'Phase', 'status.era': 'Era', 'status.engine': 'Engine',
    'badge.ready': 'Ready', 'badge.running': 'Running', 'badge.paused': 'Paused', 'badge.halted': 'Halted',
    'badge.fault': 'Fault', 'badge.breakpoint': 'Breakpoint', 'badge.error': 'Error',
    'phase.name.idle': 'idle', 'phase.name.fetch': 'fetch', 'phase.name.decode': 'decode', 'phase.name.execute': 'execute',
    'phase.name.interrupt': 'interrupt', 'phase.name.waiting': 'waiting', 'phase.name.halted': 'halted',
    'status.initial': 'Load a sample or write a program, then press Run.',
    'status.assembled': 'Program assembled and loaded. Press Run or Step.',
    'status.reset': 'Processor reset. Press Run or Step.',
    'status.running': 'Running {mode} on the {era} micro-architecture. Press Pause (F5) to stop.',
    'mode.full': 'at full speed',
    'mode.animated': 'with animation',
    'status.stepping': 'Executing one step.',
    'status.next': 'Next step at 0x{ip}{wait}.',
    'status.next.wait': ' (waiting for an interrupt)',
    'status.paused': 'Paused at 0x{ip}. Press Run to continue or Step.',
    'status.breakpoint': 'Stopped at breakpoint 0x{ip}. Press Run to continue or Step.',
    'status.halted': 'Halted after {n} instructions and {c} clock cycles.',
    'status.halted.reset': 'The processor is halted. Press Reset to run the program again.',
    'status.fault': '{fault}: {message}. Press Reset to try again.',
    'status.queued': 'Queued {n} byte(s) into the input device.',
    'stats.program': '{bytes} bytes, entry 0x{entry}, {symbols} symbols',
    'stats.modified': 'modified, assemble to load',
    'fault.InvalidOpcode': 'Invalid opcode',
    'fault.StackOverflow': 'Stack overflow',
    'fault.StackUnderflow': 'Stack underflow',
    'fault.DivideByZero': 'Division by zero',
    'fault.DivideOverflow': 'Divide overflow',
    'ref.title': 'CISC-16 instruction set',
    'ref.op': 'Op', 'ref.syntax': 'Syntax', 'ref.bytes': 'Bytes', 'ref.description': 'Description',
    'ref.intro': 'Registers: <code>A</code> and <code>R0</code>..<code>R7</code> (8 bit), <code>SI</code>, <code>DI</code>, <code>BP</code>, <code>SP</code> (16 bit), <code>FLAGS</code> = Z C N V I. Operands: <code>imm8</code> / <code>imm16</code> (number, <code>\'c\'</code> or label), <code>[addr]</code> direct, <code>[Rn]</code> zero page indirect, <code>[addr+Rn]</code> indexed, <code>[SI]</code> register indirect, <code>[BP+disp]</code> / <code>[BP-disp]</code> based. Directives: <code>.org addr</code>, <code>.db v, "text"</code>, <code>.dw word, label</code>, <code>.ds count</code>, <code>.equ NAME, value</code>. Ports: <code>OUT 0</code> number, <code>OUT 1</code> character, <code>OUT 2/3</code> timer period, <code>OUT 4</code> timer control, <code>IN A, 1</code> input, <code>IN A, 6</code> input status. Interrupt vectors live at <code>0xFFF0</code> + 2 &times; IRQ, R6 is the REP count, R7 the high byte of MUL / DIV.',
    'family.control': 'Control and interrupts', 'family.move': 'Data movement', 'family.stack': 'Stack and frames',
    'family.arith': 'Arithmetic', 'family.logic': 'Logic and shifts', 'family.flow': 'Control flow',
    'family.flags': 'Flags', 'family.string': 'String operations', 'family.io': 'Input / output',
    'guide.title': 'Guide',
    'lang.en': 'EN', 'lang.ru': 'RU',
    'tip.lang': 'Interface language',
  };

  const ru = {
    'app.title': 'CISC-16: визуализатор процессора',
    'brand.sub': 'Визуализатор процессора',
    'toolbar.program': 'Программа',
    'toolbar.era': 'Эпоха',
    'toolbar.speed': 'Скорость',
    'toolbar.engine': 'Движок',
    'era.Classic1975': '1975: многотактный',
    'era.Prefetch1985': '1985: очередь предвыборки',
    'era.Pipelined1995': '1995: конвейер и кэши',
    'era.name.Classic1975': '1975, многотактное устройство управления (как у 8080)',
    'era.name.Prefetch1985': '1985, очередь предвыборки (как у 8086)',
    'era.name.Pipelined1995': '1995, конвейер и кэши (как у Pentium)',
    'era.classic.text': 'Многотактное устройство управления: каждый цикл шины занимает несколько тактов, действия выполняются последовательно. Ряд ниже отмечает циклы шины.',
    'era.queue': 'Очередь предвыборки',
    'era.icache': 'Кэш команд',
    'era.dcache': 'Кэш данных',
    'era.predictor': 'Предсказатель переходов',
    'era.queue.empty': '(пусто)',
    'era.title': 'Микроархитектура',
    'btn.assemble': 'Ассемблировать',
    'btn.run': 'Запуск',
    'btn.pause': 'Пауза',
    'btn.step': 'Шаг',
    'btn.reset': 'Сброс',
    'btn.reference': 'Система команд',
    'btn.guide': 'Руководство',
    'btn.theme': 'Тема',
    'btn.send': 'Отправить',
    'btn.clear': 'очистить',
    'btn.close': 'Закрыть',
    'tip.assemble': 'Ассемблировать текст и загрузить его в память (Ctrl+Enter)',
    'tip.run': 'Запуск / пауза (F5)',
    'tip.step': 'Выполнить один шаг (F8)',
    'tip.reset': 'Сбросить процессор и заново загрузить программу',
    'tip.sample': 'Загрузить пример программы',
    'tip.era': 'Поколение микроархитектуры, по которому считаются такты',
    'tip.engine': 'Где исполняется программа: в браузере или на сервере .NET',
    'tip.theme': 'Переключить светлую и тёмную тему',
    'tip.guide': 'Устройство стенда и порядок написания программ',
    'tip.reference': 'Справочник системы команд',
    'tip.pagePrev': 'Предыдущая страница',
    'tip.pageNext': 'Следующая страница',
    'tip.pageIp': 'Показать страницу, на которую указывает IP',
    'tip.pageSp': 'Показать страницу, на которую указывает SP',
    'tip.follow': 'Переключать страницу на каждый адрес, к которому обращается процессор',
    'engine.js': 'JavaScript (браузер)',
    'engine.dotnet': '.NET (сервер)',
    'engine.name.js': 'JavaScript',
    'sample.custom': 'Своя программа',
    'speed.1': 'ползком', 'speed.2': 'очень медленно', 'speed.3': 'медленно', 'speed.4': 'спокойно', 'speed.5': 'обычно',
    'speed.6': 'бодро', 'speed.7': 'быстро', 'speed.8': 'быстрее', 'speed.9': 'очень быстро', 'speed.10': 'без анимации',
    'panel.source': 'Исходный текст',
    'panel.listing': 'Листинг',
    'listing.hint': 'щелчок по адресу ставит или снимает точку останова',
    'panel.devices': 'Устройства',
    'devices.hint': 'прерывания, таймер, ввод',
    'dev.irq': 'Линии прерываний',
    'dev.iflag': 'Флаг I',
    'dev.enabled': 'разрешены',
    'dev.timer': 'Таймер (IRQ 0)',
    'dev.period': 'период',
    'dev.cycles': 'тактов',
    'dev.off': 'выключен',
    'dev.running': 'работает',
    'dev.ticks': 'срабатываний',
    'dev.input': 'Устройство ввода (порты 1, 6)',
    'dev.input.placeholder': 'текст для IN A, 1',
    'dev.queue': 'очередь',
    'dev.empty': '(пусто)',
    'die.label': 'CISC-16 · данные 8 бит · адреса 16 бит',
    'cu.title': 'Устройство управления',
    'phase.fetch': 'Выборка',
    'phase.decode': 'Декодирование',
    'phase.execute': 'Исполнение',
    'phase.irq': 'IRQ',
    'phase.wait': 'Ожидание',
    'clock': 'такт',
    'clock.cycles': 'тактов',
    'clock.instructions': 'команд',
    'clock.last': 'последний шаг',
    'regs.title': 'Регистровый файл',
    'alu.title': 'АЛУ',
    'alu.result': 'результат',
    'alu.idle': 'простой',
    'bus.addr': 'адресная шина 16',
    'bus.data': 'шина данных 8',
    'mem.page': 'страница',
    'mem.follow': 'следить',
    'legend.code': 'код', 'legend.data': 'данные', 'legend.stack': 'стек', 'legend.vectors': 'векторы', 'legend.ip': 'IP',
    'panel.output': 'Порты вывода',
    'output.hint': 'порт 0 = числа, порт 1 = символы',
    'panel.trace': 'Журнал исполнения',
    'trace.cyc': 'такт',
    'status.ip': 'IP', 'status.sp': 'SP', 'status.phase': 'Фаза', 'status.era': 'Эпоха', 'status.engine': 'Движок',
    'badge.ready': 'Готов', 'badge.running': 'Работает', 'badge.paused': 'Пауза', 'badge.halted': 'Остановлен',
    'badge.fault': 'Сбой', 'badge.breakpoint': 'Точка останова', 'badge.error': 'Ошибка',
    'phase.name.idle': 'простой', 'phase.name.fetch': 'выборка', 'phase.name.decode': 'декодирование', 'phase.name.execute': 'исполнение',
    'phase.name.interrupt': 'прерывание', 'phase.name.waiting': 'ожидание', 'phase.name.halted': 'остановлен',
    'status.initial': 'Выберите пример или напишите программу, затем нажмите Запуск.',
    'status.assembled': 'Программа ассемблирована и загружена. Нажмите Запуск или Шаг.',
    'status.reset': 'Процессор сброшен. Нажмите Запуск или Шаг.',
    'status.running': 'Исполнение {mode} на микроархитектуре {era}. Пауза: F5.',
    'mode.full': 'без анимации',
    'mode.animated': 'с анимацией',
    'status.stepping': 'Исполняется один шаг.',
    'status.next': 'Следующий шаг по адресу 0x{ip}{wait}.',
    'status.next.wait': ' (ожидание прерывания)',
    'status.paused': 'Пауза по адресу 0x{ip}. Продолжение: Запуск или Шаг.',
    'status.breakpoint': 'Остановка в точке 0x{ip}. Продолжение: Запуск или Шаг.',
    'status.halted': 'Остановлен после {n} команд и {c} тактов.',
    'status.halted.reset': 'Процессор остановлен. Для повторного запуска нажмите Сброс.',
    'status.fault': '{fault}: {message}. Для повтора нажмите Сброс.',
    'status.queued': 'В очередь ввода добавлено байт: {n}.',
    'stats.program': '{bytes} байт, вход 0x{entry}, символов: {symbols}',
    'stats.modified': 'изменено, требуется ассемблирование',
    'fault.InvalidOpcode': 'Недопустимый код операции',
    'fault.StackOverflow': 'Переполнение стека',
    'fault.StackUnderflow': 'Чтение из пустого стека',
    'fault.DivideByZero': 'Деление на ноль',
    'fault.DivideOverflow': 'Переполнение при делении',
    'ref.title': 'Система команд CISC-16',
    'ref.op': 'Код', 'ref.syntax': 'Синтаксис', 'ref.bytes': 'Байт', 'ref.description': 'Описание',
    'ref.intro': 'Регистры: <code>A</code> и <code>R0</code>..<code>R7</code> (8 разрядов), <code>SI</code>, <code>DI</code>, <code>BP</code>, <code>SP</code> (16 разрядов), <code>FLAGS</code> = Z C N V I. Операнды: <code>imm8</code> / <code>imm16</code> (число, <code>\'c\'</code> или метка), <code>[addr]</code> прямая адресация, <code>[Rn]</code> косвенная через нулевую страницу, <code>[addr+Rn]</code> индексная, <code>[SI]</code> косвенная регистровая, <code>[BP+disp]</code> / <code>[BP-disp]</code> базовая. Директивы: <code>.org addr</code>, <code>.db v, "text"</code>, <code>.dw word, label</code>, <code>.ds count</code>, <code>.equ NAME, value</code>. Порты: <code>OUT 0</code> число, <code>OUT 1</code> символ, <code>OUT 2/3</code> период таймера, <code>OUT 4</code> управление таймером, <code>IN A, 1</code> ввод, <code>IN A, 6</code> состояние ввода. Векторы прерываний расположены по адресу <code>0xFFF0</code> + 2 &times; IRQ, R6 хранит счётчик REP, R7 старший байт MUL / DIV.',
    'family.control': 'Управление и прерывания', 'family.move': 'Пересылки данных', 'family.stack': 'Стек и кадры',
    'family.arith': 'Арифметика', 'family.logic': 'Логика и сдвиги', 'family.flow': 'Передача управления',
    'family.flags': 'Флаги', 'family.string': 'Строковые операции', 'family.io': 'Ввод и вывод',
    'guide.title': 'Руководство',
    'lang.en': 'EN', 'lang.ru': 'RU',
    'tip.lang': 'Язык интерфейса',
  };

  /** Russian names of the timing statistics reported by the engines. */
  const statsRu = {
    'bus cycles': 'циклов шины', 'ALU operations': 'операций АЛУ', 'branches taken': 'переходов выполнено',
    'branches not taken': 'переходов пропущено', 'interrupts': 'прерываний', 'queue hits': 'попаданий в очередь',
    'queue misses': 'промахов очереди', 'queue flushes': 'сбросов очереди', 'words prefetched': 'слов предвыбрано',
    'I-cache hits': 'попаданий в кэш команд', 'I-cache misses': 'промахов кэша команд',
    'D-cache hits': 'попаданий в кэш данных', 'D-cache misses': 'промахов кэша данных',
    'branches predicted': 'переходов предсказано', 'branches mispredicted': 'ошибок предсказания',
    'instructions issued': 'команд выдано', 'port transfers': 'обменов с портами',
  };

  /** Russian templates for the timing events; {addr}, {line}, {value} are filled in by the page. */
  const eventsRu = {
    'queue hit': 'байт 0x{addr} взят из очереди предвыборки',
    'queue miss': 'байт 0x{addr}: ожидание блока шины, тактов: {value}',
    'queue flush': 'переход выполнен: очередь предвыборки сброшена, выборка с 0x{addr}',
    'I-cache hit': 'попадание в кэш команд, строка {line}',
    'I-cache miss': 'промах кэша команд, строка {line} заполнена из памяти',
    'D-cache hit': 'попадание в кэш данных, строка {line}',
    'D-cache miss': 'промах кэша данных, строка {line} заполнена из памяти',
    'predicted': 'переход предсказан верно',
    'mispredicted': 'ошибка предсказания перехода, конвейер сброшен',
    'multi-cycle': 'многотактная операция АЛУ',
    'I/O': 'обмен с портом без кэша',
    'interrupt': 'подтверждение прерывания',
  };

  /** Russian names and descriptions of the sample programs (sources stay as they are). */
  const samplesRu = {
    countdown: ['Обратный отсчёт', 'Считает от 5 до 1 одной командой LOOP и печатает каждое значение.'],
    sum: ['Сумма массива', 'Складывает шесть байт, расположенных после кода, через индексную адресацию [data+R1].'],
    fibonacci: ['Числа Фибоначчи', 'Печатает последовательность до переполнения 8 разрядов; цикл завершает флаг переноса.'],
    factorial: ['Факториал (CALL / RET / MUL)', 'Вычисляет 5! в подпрограмме, которая хранит рабочий регистр в стеке.'],
    hello: ['Hello, CISC!', 'Проходит строку с нулевым завершителем командой LODSB и печатает её в символьный порт.'],
    stack: ['Стек и подпрограммы', 'Меняет два регистра местами через стек и удваивает значения в подпрограмме.'],
    sort: ['Пузырьковая сортировка', 'Сортирует шесть байт на месте вложенными LOOP и индексными записями, затем печатает их. Подходит для сравнения трёх эпох.'],
    strings: ['Строковые команды (REP MOVSB)', 'Копирует строку одной командой REP MOVSB и печатает копию циклом с LODSB.'],
    scan: ['Заполнение и поиск (REP STOSB / SCASB)', 'Заполняет буфер командой REP STOSB, ставит маркер и находит его командой REP SCASB.'],
    frames: ['Кадры стека (ENTER / LEAVE / [BP+disp])', 'Вызывает функцию с двумя аргументами в стеке и локальной переменной, как это делает компилятор.'],
    jumptable: ['Таблица переходов (JMP [SI])', 'Выбирает подпрограмму по таблице 16-разрядных адресов косвенным переходом.'],
    interrupt: ['Прерывание от таймера', 'Таймер поднимает IRQ 0; обработчик считает срабатывания, основная программа ждёт в HLT.'],
    echo: ['Эхо ввода (IN)', 'Читает устройство ввода до опустошения очереди и печатает каждый символ в верхнем регистре.'],
    fault: ['Аппаратный сбой', 'Делит на нулевой регистр; процессор останавливается со сбоем DivideByZero.'],
  };

  /** Russian descriptions of every instruction, by opcode. */
  const instructionsRu = {
    0x00: 'Нет операции',
    0x01: 'Останов; при разрешённых прерываниях ожидание следующего прерывания',
    0x02: 'Разрешить маскируемые прерывания (установить I)',
    0x03: 'Запретить маскируемые прерывания (сбросить I)',
    0x04: 'Возврат из обработчика прерывания (снять со стека IP, затем FLAGS)',
    0x05: 'Программное прерывание через вектор imm8 (0..7)',
    0x10: 'Загрузить константу в A',
    0x11: 'Загрузить константу в Rn',
    0x12: 'Скопировать Rn в A',
    0x13: 'Скопировать A в Rn',
    0x14: 'Скопировать Rm в Rn',
    0x15: 'Загрузить A из памяти (прямая адресация, 16-разрядный адрес)',
    0x16: 'Сохранить A в память (прямая адресация)',
    0x17: 'Загрузить A из нулевой страницы по адресу Rn (косвенная регистровая)',
    0x18: 'Сохранить A в нулевую страницу по адресу Rn (косвенная регистровая)',
    0x19: 'Загрузить A по адресу addr + Rn (индексная)',
    0x1A: 'Сохранить A по адресу addr + Rn (индексная)',
    0x1B: 'Загрузить Rn из памяти (прямая адресация)',
    0x1C: 'Сохранить Rn в память (прямая адресация)',
    0x1D: 'Загрузить A по адресу из SI / DI / BP / SP',
    0x1E: 'Сохранить A по адресу из SI / DI / BP / SP',
    0x1F: 'Загрузить A по адресу XR + знаковое смещение (базовая)',
    0x20: 'Сохранить A по адресу XR + знаковое смещение (базовая)',
    0x21: 'Обменять A и Rn',
    0x22: 'Загрузить 16-разрядную константу в SI / DI / BP / SP',
    0x23: 'Скопировать один адресный регистр в другой',
    0x24: 'Загрузить 16-разрядное слово (младший байт первым) в адресный регистр',
    0x25: 'Сохранить адресный регистр как 16-разрядное слово',
    0x26: 'XR = XS + disp8 (вычисление адреса без обращения к памяти)',
    0x27: 'Загрузить Rn по адресу XR + знаковое смещение (базовая)',
    0x28: 'Сохранить Rn по адресу XR + знаковое смещение (базовая)',
    0x29: 'XR = XR + A (A расширяется нулями), индексация таблиц',
    0x30: 'Поместить A в стек',
    0x31: 'Поместить Rn в стек',
    0x32: 'Поместить регистр флагов в стек',
    0x33: 'Поместить адресный регистр в стек (16 разрядов)',
    0x34: 'Снять со стека в A',
    0x35: 'Снять со стека в Rn',
    0x36: 'Снять со стека в регистр флагов',
    0x37: 'Снять со стека в адресный регистр (16 разрядов)',
    0x38: 'Поместить BP в стек, BP = SP, SP = SP - imm8 (кадр стека с imm8 байтами локальных переменных)',
    0x39: 'SP = BP, снять BP со стека (удалить кадр стека, созданный ENTER)',
    0x40: 'A = A + imm8', 0x41: 'A = A + Rn', 0x42: 'A = A + [addr]', 0x43: 'A = A + [XR]',
    0x44: 'A = A + imm8 + C', 0x45: 'A = A + Rn + C', 0x46: 'A = A + [addr] + C',
    0x47: 'A = A - imm8', 0x48: 'A = A - Rn', 0x49: 'A = A - [addr]', 0x4A: 'A = A - [XR]',
    0x4B: 'A = A - imm8 - C', 0x4C: 'A = A - Rn - C', 0x4D: 'A = A - [addr] - C',
    0x4E: 'Флаги по результату A - imm8', 0x4F: 'Флаги по результату A - Rn',
    0x50: 'Флаги по результату A - [addr]', 0x51: 'Флаги по результату A - [XR]',
    0x52: 'A = A + 1 (C сохраняется)', 0x53: 'Rn = Rn + 1 (C сохраняется)', 0x54: '[addr] = [addr] + 1 (чтение, изменение, запись)',
    0x55: 'A = A - 1 (C сохраняется)', 0x56: 'Rn = Rn - 1 (C сохраняется)', 0x57: '[addr] = [addr] - 1 (чтение, изменение, запись)',
    0x58: 'R7:A = A * Rn (16-разрядное произведение, старший байт в R7)',
    0x59: 'A = R7:A / Rn, R7 = остаток (без знака); сбой при делителе 0 и при переполнении',
    0x5A: 'A = -A (дополнительный код)',
    0x5B: 'XR = XR + imm16 (16 разрядов)', 0x5C: 'XR = XR - imm16 (16 разрядов)', 0x5D: 'Флаги по результату XR - imm16 (16 разрядов)',
    0x5E: 'XR = XR + 1 (C сохраняется)', 0x5F: 'XR = XR - 1 (C сохраняется)',
    0x60: 'A = A and imm8', 0x61: 'A = A and Rn', 0x62: 'A = A or imm8', 0x63: 'A = A or Rn',
    0x64: 'A = A xor imm8', 0x65: 'A = A xor Rn', 0x66: 'A = not A',
    0x67: 'Сдвиг A влево, C = старый бит 7', 0x68: 'Сдвиг A вправо, C = старый бит 0',
    0x69: 'Циклический сдвиг A влево, C = старый бит 7', 0x6A: 'Циклический сдвиг A вправо, C = старый бит 0',
    0x70: 'Безусловный переход', 0x71: 'Переход при Z = 1', 0x72: 'Переход при Z = 0', 0x73: 'Переход при C = 1',
    0x74: 'Переход при C = 0', 0x75: 'Переход при N = 1', 0x76: 'Переход при N = 0', 0x77: 'Переход при V = 1', 0x78: 'Переход при V = 0',
    0x79: 'Поместить адрес возврата в стек и перейти к подпрограмме',
    0x7A: 'Возврат из подпрограммы',
    0x7B: 'Уменьшить Rn на 1 и перейти по addr, пока Rn отличен от нуля',
    0x7C: 'Переход по слову, хранящемуся по адресу [XR] (таблицы переходов)',
    0x80: 'Сбросить флаг переноса', 0x81: 'Установить флаг переноса',
    0x90: 'A = [SI], SI = SI + 1', 0x91: '[DI] = A, DI = DI + 1', 0x92: '[DI] = [SI], SI и DI увеличиваются на 1',
    0x93: 'Флаги по результату [SI] - [DI], SI и DI увеличиваются на 1', 0x94: 'Флаги по результату A - [DI], DI увеличивается на 1',
    0x95: 'Скопировать R6 байт из [SI] в [DI]', 0x96: 'Заполнить R6 байт по адресу [DI] значением A',
    0x97: 'Сравнить до R6 байт, остановка на первом различии (REPE)',
    0x98: 'Просмотреть до R6 байт по адресу [DI] в поиске значения A, остановка на первом совпадении (REPNE)',
    0xF0: 'Записать A в порт вывода (0 = число, 1 = символ, 2..4 = таймер)',
    0xF1: 'Прочитать порт ввода в A (0 / 1 = очередь ввода, 5 = счётчик таймера, 6 = состояние ввода)',
  };

  const guideEn = [
    { h: 'What the stand is', html: `
<p>CISC-16 is a software model of a processor with a CISC instruction set. The model executes machine code produced by the built-in assembler and displays every action of the control unit: bus transfers, ALU operations, register writes, interrupt acknowledges, port transfers.</p>
<p>The architecture follows the 8 bit machines with 16 bit addressing of the 1970s (Intel 8080, Zilog Z80) and takes index registers, string instructions, stack frames and software interrupts from the Intel 8086. The timing follows one of three generations of micro-architecture: 1975, 1985 or 1995. A program produces the same results on all three. The cycle counts and the events shown in the micro-architecture block depend on the selected era.</p>
<p>Two implementations exist with identical behaviour: the C# library <code>CISCProcessorLibrary</code> and the JavaScript mirror built into this page. The page works without a server. When the page is served by <code>CISCProcessorWeb</code>, the toolbar shows an <em>Engine</em> selector and the program can run on the .NET core.</p>` },
    { h: 'Screen layout', html: `
<table><thead><tr><th>Area</th><th>Content</th></tr></thead><tbody>
<tr><td>Source</td><td>Editor for the assembly program. Line numbers on the left; a line with an assembler error is marked in red and the message appears under the editor.</td></tr>
<tr><td>Listing</td><td>Result of assembling: address, bytes and source line. The current instruction is highlighted, the next one is tinted. A click on an address sets or clears a breakpoint.</td></tr>
<tr><td>Devices</td><td>Eight interrupt request lines, the I flag, the timer, the input device with a text field.</td></tr>
<tr><td>Die</td><td>Control unit with the cycle phases, register IR with the decoded instruction, cycle counter, register file, ALU with the flags, micro-architecture block.</td></tr>
<tr><td>Buses</td><td>16 bit address bus (amber) and 8 bit data bus (cyan). A packet travels along a bus on every memory access.</td></tr>
<tr><td>Memory</td><td>A 256 byte window on one page of the 64 KB address space. Page buttons, the IP and SP buttons and the follow mode select the page.</td></tr>
<tr><td>Output ports</td><td>Bytes written by <code>OUT</code> to port 0 (numbers) and port 1 (characters).</td></tr>
<tr><td>Execution trace</td><td>Address, bytes, instruction and cycle count of every step.</td></tr>
<tr><td>Status bar</td><td>Processor state, IP, SP, phase, era, engine.</td></tr>
</tbody></table>` },
    { h: 'Architecture', html: `
<table><thead><tr><th>Register</th><th>Width</th><th>Role</th></tr></thead><tbody>
<tr><td><code>A</code></td><td>8</td><td>Accumulator, implicit operand of every 8 bit ALU instruction. <code>MUL</code> and <code>DIV</code> use <code>R7:A</code> as a 16 bit value.</td></tr>
<tr><td><code>R0</code>..<code>R7</code></td><td>8</td><td>General purpose registers. <code>R6</code> is the count of the <code>REP</code> instructions, <code>R7</code> the high byte of <code>MUL</code> / <code>DIV</code>.</td></tr>
<tr><td><code>SI</code>, <code>DI</code></td><td>16</td><td>Source and destination index: pointers for the string instructions and for <code>[SI]</code>, <code>[DI+disp]</code>.</td></tr>
<tr><td><code>BP</code></td><td>16</td><td>Base pointer of the current stack frame (<code>ENTER</code>, <code>LEAVE</code>, <code>[BP+disp]</code>).</td></tr>
<tr><td><code>SP</code></td><td>16</td><td>Stack pointer. The stack lives in main memory and grows downwards from <code>0xFFF0</code>.</td></tr>
<tr><td><code>IP</code></td><td>16</td><td>Instruction pointer. Execution starts at the first instruction of the program.</td></tr>
<tr><td><code>FLAGS</code></td><td>5 bits</td><td><code>Z</code> zero, <code>C</code> carry or borrow, <code>N</code> negative, <code>V</code> signed overflow, <code>I</code> interrupts enabled.</td></tr>
<tr><td><code>IR</code>, <code>MAR</code>, <code>MDR</code></td><td>8 / 16 / 8</td><td>Instruction register, memory address register and memory data register: the internal registers that drive the buses.</td></tr>
</tbody></table>
<p>Memory: 65536 bytes, byte addressable, 16 bit words stored low byte first. <code>[Rn]</code> addressing reaches the first 256 bytes (the zero page). The interrupt vector table occupies <code>0xFFF0</code>..<code>0xFFFF</code>: eight 16 bit handler addresses, IRQ <em>n</em> at <code>0xFFF0 + 2n</code>. The stack starts right below the table.</p>
<p>Flag rules: <code>ADD</code> sets C on an unsigned carry and V on a signed overflow; <code>SUB</code> and <code>CMP</code> set C on a borrow; logic instructions clear C and V; shifts move the outgoing bit into C; <code>INC</code> and <code>DEC</code> keep C; <code>MOV</code>, <code>PUSH</code>, <code>POP</code> and <code>LOOP</code> leave the flags alone. The ALU never touches I.</p>
<p>Faults stop the processor with a message: invalid opcode, stack overflow, stack underflow, division by zero, divide overflow (a quotient above 255).</p>` },
    { h: 'Instruction cycle and animation', html: `
<p>Each instruction passes through three phases. <strong>Fetch</strong>: the opcode at IP is read into IR, IP advances. <strong>Decode</strong>: the opcode is looked up in the instruction table, the operand bytes are read, the decoded text appears next to IR. <strong>Execute</strong>: the operation runs on the data path.</p>
<p>Every memory access is shown as two packets. An amber packet travels from MAR along the address bus to the memory cell. A cyan packet then travels along the data bus: from the cell to MDR on a read, from MDR to the cell on a write. The cell flashes cyan on a read and amber on a write. A register flashes when its value changes. The ALU shows its operands, the operation and the result; the flag LEDs follow the result.</p>
<p>Two kinds of steps exist besides instructions. <strong>IRQ</strong>: the control unit acknowledges a pending interrupt, pushes FLAGS and IP, clears I and jumps through the vector table; the trace shows the line and the handler address. <strong>Wait</strong>: after <code>HLT</code> with I set the processor idles one quantum per step until an interrupt arrives.</p>
<p>The speed slider sets the pause between micro-operations. The rightmost position disables the animation: the engine runs hundreds of steps per frame and the views refresh at the end of each batch.</p>` },
    { h: 'Writing and running a program', html: `
<ol>
<li>Select <em>Custom program</em> in the <em>Program</em> list, or pick a sample and edit it. Editing marks the program as modified.</li>
<li>Type the program in the <em>Source</em> panel. Tab inserts eight spaces.</li>
<li>Press <em>Assemble</em> (Ctrl+Enter). The listing shows the addresses and bytes; the memory window shows the loaded code in blue and data in green. An error names the line and the reason; the line is marked in the editor.</li>
<li>Press <em>Run</em> (F5) for continuous execution or <em>Step</em> (F8) for one step at a time. <em>Run</em> turns into <em>Pause</em> while the processor runs; Esc also pauses.</li>
<li>A click on an address in the listing sets a breakpoint: continuous execution stops in front of that instruction every time IP reaches it.</li>
<li><em>Reset</em> reloads the program: registers, memory, devices and the timing model return to their initial state. A program that executed <code>HLT</code> restarts from the beginning on the next <em>Run</em>.</li>
<li>Programs that read the input device take their bytes from the text field in the <em>Devices</em> panel. The field is queued on every load; the <em>Send</em> button appends more text while the program runs.</li>
</ol>
<p>A minimal program:</p>
<pre>        MOV A, 40
        ADD A, 2
        OUT 0, A        ; prints 42
        HLT</pre>` },
    { h: 'Assembly language', html: `
<p>One statement per line: <code>[label:] MNEMONIC [operand[, operand]] [; comment]</code>. Mnemonics, register names and labels are case insensitive. A label marks the address of the following statement and can be used anywhere a number is expected, before or after its definition.</p>
<table><thead><tr><th>Operand</th><th>Meaning</th><th>Example</th></tr></thead><tbody>
<tr><td><code>A</code>, <code>R0</code>..<code>R7</code></td><td>8 bit registers</td><td><code>MOV A, R3</code></td></tr>
<tr><td><code>SI</code>, <code>DI</code>, <code>BP</code>, <code>SP</code></td><td>16 bit address registers</td><td><code>MOV SI, buffer</code></td></tr>
<tr><td><code>FLAGS</code></td><td>flag register (push and pop only)</td><td><code>PUSH FLAGS</code></td></tr>
<tr><td>number, label</td><td>immediate value (8 or 16 bit) or jump target</td><td><code>MOV A, 0x2A</code>, <code>JMP loop</code></td></tr>
<tr><td><code>[addr]</code></td><td>direct: the byte (or word) at a 16 bit address</td><td><code>MOV A, [0x1234]</code></td></tr>
<tr><td><code>[Rn]</code></td><td>zero page indirect: the byte at address Rn (0..255)</td><td><code>MOV [R0], A</code></td></tr>
<tr><td><code>[addr+Rn]</code></td><td>indexed: the byte at addr + Rn</td><td><code>MOV A, [table+R1]</code></td></tr>
<tr><td><code>[SI]</code>, <code>[DI]</code>, <code>[BP]</code>, <code>[SP]</code></td><td>register indirect: the byte at the address in the register</td><td><code>ADD A, [SI]</code></td></tr>
<tr><td><code>[BP+disp]</code>, <code>[BP-disp]</code></td><td>based: the byte at the register plus a signed displacement (-128..127)</td><td><code>MOV A, [BP+4]</code></td></tr>
</tbody></table>
<p>Numbers: decimal <code>42</code>, hexadecimal <code>0x2A</code> or <code>$2A</code>, binary <code>0b101010</code>, character <code>'A'</code>, negative <code>-1</code> (two's complement). Terms combine with <code>+</code> and <code>-</code>: <code>[table+2]</code>, <code>'z'+1</code>.</p>
<p>Directives: <code>.org addr</code> sets the location counter; <code>.db v, v, "text"</code> emits bytes; <code>.dw word, label</code> emits 16 bit words, low byte first; <code>.ds count</code> reserves zeroed bytes; <code>.equ NAME, value</code> defines a constant. The entry point is the first instruction in the text, so data placed before the code is skipped.</p>` },
    { h: 'Instruction groups', html: `
<p><strong>Data movement.</strong> <code>MOV</code> copies a byte or a word between registers, memory and constants; the width follows the operands (<code>SI</code>, <code>DI</code>, <code>BP</code>, <code>SP</code> and <code>imm16</code> are 16 bit). <code>XCHG A, Rn</code> swaps two registers. <code>LEA XR, [XS+disp]</code> stores the computed address itself. None of them change the flags.</p>
<p><strong>Arithmetic.</strong> <code>ADD</code>, <code>ADC</code> (plus carry), <code>SUB</code>, <code>SBB</code> (minus borrow) and <code>CMP</code> combine A with a constant, a register or a memory byte; <code>CMP</code> updates the flags only. <code>INC</code>, <code>DEC</code> and <code>NEG</code> work on one operand, also directly in memory. <code>MUL Rn</code> forms the 16 bit product <code>R7:A</code>; <code>DIV Rn</code> divides <code>R7:A</code> by Rn, leaves the quotient in A and the remainder in R7. <code>ADD</code>, <code>SUB</code>, <code>CMP</code>, <code>INC</code>, <code>DEC</code> with an address register are 16 bit operations; <code>ADD XR, A</code> adds the accumulator to an address register for table indexing.</p>
<p><strong>Logic and shifts.</strong> <code>AND</code>, <code>OR</code>, <code>XOR</code>, <code>NOT</code> clear C and V. <code>SHL</code>, <code>SHR</code> shift A by one bit and place the outgoing bit in C; <code>ROL</code>, <code>ROR</code> rotate A and copy the rotated bit into C.</p>
<p><strong>Control flow.</strong> <code>JMP addr</code> jumps; <code>JZ</code>, <code>JNZ</code>, <code>JC</code>, <code>JNC</code>, <code>JS</code>, <code>JNS</code>, <code>JO</code>, <code>JNO</code> jump on a flag. <code>CALL addr</code> pushes the 16 bit return address and jumps; <code>RET</code> pops it. <code>LOOP Rn, addr</code> decrements Rn and jumps while Rn stays above zero. <code>JMP [XR]</code> reads a 16 bit target from memory: the base of jump tables.</p>
<p><strong>Stack and frames.</strong> <code>PUSH</code> decrements SP and stores; <code>POP</code> loads and increments. Address registers are pushed as words, A, Rn and FLAGS as bytes. <code>ENTER n</code> pushes BP, copies SP to BP and reserves n bytes of locals; <code>LEAVE</code> restores SP and BP. Inside a frame <code>[BP+2]</code> holds the return address, <code>[BP+4]</code> the first argument pushed by the caller, <code>[BP-1]</code> the first local.</p>
<p><strong>String operations.</strong> <code>LODSB</code> loads A from [SI], <code>STOSB</code> stores A at [DI], <code>MOVSB</code> copies [SI] to [DI], <code>CMPSB</code> compares [SI] with [DI], <code>SCASB</code> compares A with [DI]; the index registers advance by one. The <code>REP</code> forms repeat the operation R6 times; <code>REP CMPSB</code> stops at the first difference, <code>REP SCASB</code> at the first match.</p>
<p><strong>Interrupts.</strong> <code>EI</code> and <code>DI</code> set and clear I. A pending request on a line is acknowledged between instructions when I is set. <code>INT n</code> triggers vector n from the program. <code>IRET</code> returns from a handler. <code>HLT</code> with I set waits for the next interrupt; with I clear it halts.</p>
<p><strong>Input and output.</strong> <code>OUT port, A</code> writes A to a port, <code>IN A, port</code> reads a port into A. Ports without a device return <code>0xFF</code>.</p>
<p><strong>Flags and NOP.</strong> <code>CLC</code> and <code>STC</code> clear and set C; <code>NOP</code> takes a cycle and does nothing else.</p>
<p>The <em>Instruction set</em> button opens the full table with opcodes, syntax and byte counts.</p>` },
    { h: 'Micro-architecture eras', html: `
<table><thead><tr><th>Era</th><th>Model</th><th>Block contents</th></tr></thead><tbody>
<tr><td>1975</td><td>Multi-cycle control unit in the style of the 8080: 4 T-states for the opcode fetch, 3 for every other memory byte, extra cycles for INC / DEC / shifts, 16 for MUL, 24 for DIV, 11 for an interrupt acknowledge. Bus cycles run one after another.</td><td>A row of bus cycles and the counters.</td></tr>
<tr><td>1985</td><td>8086 style bus interface unit: a 6 byte prefetch queue filled by 16 bit bus cycles while the execution unit works. A byte taken from the queue costs 1 cycle; a byte missing from the queue makes the execution unit wait for the bus. A taken jump flushes the queue. Data and port transfers take 4 cycles, ALU operations 2, MUL 30, DIV 40, an interrupt 40.</td><td>The queue with its bytes and address range, hits, misses, flushes.</td></tr>
<tr><td>1995</td><td>Pentium style pipeline: one simple instruction per cycle. Direct-mapped instruction and data caches of 16 lines by 16 bytes; a miss stalls for 8 cycles. A 16 entry branch target buffer with 2 bit counters; a wrong prediction costs 3 cycles. MUL takes 10 cycles, DIV 40, an uncached port transfer 8, an interrupt 20.</td><td>Both caches line by line (the page number of the cached line), the predictor counters, hits, misses, predictions.</td></tr>
</tbody></table>
<p>The era applies to the whole run; a change in the <em>Era</em> list reloads the program. The cycle counter in the control unit and the per-step counts in the trace follow the selected model.</p>` },
    { h: 'Devices and ports', html: `
<table><thead><tr><th>Port</th><th><code>OUT port, A</code></th><th><code>IN A, port</code></th></tr></thead><tbody>
<tr><td>0</td><td>print A as a decimal number</td><td>next byte of the input queue</td></tr>
<tr><td>1</td><td>print A as a character</td><td>next character of the input queue, 0 when the queue is empty</td></tr>
<tr><td>2, 3</td><td>timer period, low and high byte, in clock cycles</td><td>read back</td></tr>
<tr><td>4</td><td>timer control: bit 0 starts the timer</td><td>1 while the timer runs</td></tr>
<tr><td>5</td><td></td><td>timer tick count, low byte</td></tr>
<tr><td>6</td><td></td><td>1 when a byte is waiting in the input queue</td></tr>
</tbody></table>
<p>The timer counts the clock cycles of every step and raises IRQ 0 each time the period elapses. The handler address comes from the vector table; a program places it there with <code>.org 0xFFF0</code> and <code>.dw handler</code>. The <em>Timer interrupt</em> sample shows the complete pattern.</p>
<p>The input device holds a queue of bytes. The text field in the <em>Devices</em> panel is queued on every load of the program; <em>Send</em> appends text at any moment. The <em>Input echo</em> sample reads the queue until port 6 reports it empty.</p>` },
    { h: 'Memory window', html: `
<p>The window shows one 256 byte page; the row labels are full 16 bit addresses. Colours: blue cells hold code, green cells hold data from <code>.db</code> / <code>.dw</code> / <code>.ds</code>, violet cells belong to the stack (from SP up to <code>0xFFEF</code>), cyan cells form the vector table, an amber outline marks the bytes of the current instruction, an underline marks SP.</p>
<p>With <em>follow</em> enabled the window switches to the page of every address the processor touches, so a push shows the stack page and the next fetch returns to the code page. With <em>follow</em> disabled the page changes only through the arrow buttons and the <em>IP</em> and <em>SP</em> buttons. A tooltip on a cell shows its address and value in hex, decimal and as a character.</p>` },
    { h: 'Engines, keys, address parameters', html: `
<p>The JavaScript engine runs in the browser and needs no server. The .NET engine appears in the <em>Engine</em> list when the page is served by <code>CISCProcessorWeb</code>; each step is then a request to the server, and the traces have the same shape. Both engines produce identical results and cycle counts.</p>
<p>Keys: F5 run or pause, F8 step, Ctrl+Enter assemble, Esc pause, Tab in the editor inserts spaces.</p>
<p>Address parameters: <code>?program=sort</code> selects a sample, <code>&amp;era=Pipelined1995</code> selects an era (<code>Classic1975</code>, <code>Prefetch1985</code>, <code>Pipelined1995</code>), <code>&amp;lang=ru</code> selects the interface language. The language, era, speed and theme are remembered in the browser.</p>` },
  ];

  const guideRu = [
    { h: 'Назначение стенда', html: `
<p>CISC-16 - программная модель процессора с системой команд класса CISC. Модель исполняет машинный код, который собирает встроенный ассемблер, и отображает каждое действие устройства управления: обращения к шинам, операции АЛУ, запись регистров, подтверждение прерываний, обмен с портами.</p>
<p>Архитектура повторяет 8-разрядные машины с 16-разрядной адресацией 1970-х годов (Intel 8080, Zilog Z80) и заимствует у Intel 8086 индексные регистры, строковые команды, кадры стека и программные прерывания. Тайминг соответствует одному из трёх поколений микроархитектуры: 1975, 1985 или 1995. Результат программы одинаков на всех трёх. Число тактов и события в блоке микроархитектуры зависят от выбранной эпохи.</p>
<p>Реализаций две, с одинаковым поведением: библиотека <code>CISCProcessorLibrary</code> на C# и JavaScript-зеркало, встроенное в эту страницу. Страница работает без сервера. При запуске через <code>CISCProcessorWeb</code> в панели инструментов появляется список <em>Движок</em>, и программа может исполняться на ядре .NET.</p>` },
    { h: 'Состав экрана', html: `
<table><thead><tr><th>Область</th><th>Содержимое</th></tr></thead><tbody>
<tr><td>Исходный текст</td><td>Редактор программы на ассемблере. Слева номера строк; строка с ошибкой ассемблера отмечается красным, сообщение выводится под редактором.</td></tr>
<tr><td>Листинг</td><td>Результат ассемблирования: адрес, байты, строка исходного текста. Текущая команда выделена, следующая подкрашена. Щелчок по адресу ставит или снимает точку останова.</td></tr>
<tr><td>Устройства</td><td>Восемь линий запроса прерывания, флаг I, таймер, устройство ввода с текстовым полем.</td></tr>
<tr><td>Кристалл</td><td>Устройство управления с фазами цикла, регистр IR с декодированной командой, счётчик тактов, регистровый файл, АЛУ с флагами, блок микроархитектуры.</td></tr>
<tr><td>Шины</td><td>Адресная шина 16 разрядов (жёлтая) и шина данных 8 разрядов (голубая). При каждом обращении к памяти по шине проходит пакет.</td></tr>
<tr><td>Память</td><td>Окно размером 256 байт на одну страницу адресного пространства 64 КБ. Страницу выбирают кнопки со стрелками, кнопки IP и SP и режим слежения.</td></tr>
<tr><td>Порты вывода</td><td>Байты, записанные командой <code>OUT</code> в порт 0 (числа) и порт 1 (символы).</td></tr>
<tr><td>Журнал исполнения</td><td>Адрес, байты, команда и число тактов каждого шага.</td></tr>
<tr><td>Строка состояния</td><td>Состояние процессора, IP, SP, фаза, эпоха, движок.</td></tr>
</tbody></table>` },
    { h: 'Архитектура', html: `
<table><thead><tr><th>Регистр</th><th>Разрядность</th><th>Назначение</th></tr></thead><tbody>
<tr><td><code>A</code></td><td>8</td><td>Аккумулятор, неявный операнд каждой 8-разрядной команды АЛУ. <code>MUL</code> и <code>DIV</code> используют <code>R7:A</code> как 16-разрядное значение.</td></tr>
<tr><td><code>R0</code>..<code>R7</code></td><td>8</td><td>Регистры общего назначения. <code>R6</code> хранит счётчик команд <code>REP</code>, <code>R7</code> старший байт <code>MUL</code> / <code>DIV</code>.</td></tr>
<tr><td><code>SI</code>, <code>DI</code></td><td>16</td><td>Индексы источника и приёмника: указатели для строковых команд и для адресации <code>[SI]</code>, <code>[DI+disp]</code>.</td></tr>
<tr><td><code>BP</code></td><td>16</td><td>Базовый указатель текущего кадра стека (<code>ENTER</code>, <code>LEAVE</code>, <code>[BP+disp]</code>).</td></tr>
<tr><td><code>SP</code></td><td>16</td><td>Указатель стека. Стек расположен в основной памяти и растёт вниз от <code>0xFFF0</code>.</td></tr>
<tr><td><code>IP</code></td><td>16</td><td>Указатель команды. Исполнение начинается с первой команды программы.</td></tr>
<tr><td><code>FLAGS</code></td><td>5 бит</td><td><code>Z</code> ноль, <code>C</code> перенос или заём, <code>N</code> отрицательный результат, <code>V</code> знаковое переполнение, <code>I</code> прерывания разрешены.</td></tr>
<tr><td><code>IR</code>, <code>MAR</code>, <code>MDR</code></td><td>8 / 16 / 8</td><td>Регистр команды, регистр адреса памяти и регистр данных памяти: внутренние регистры, управляющие шинами.</td></tr>
</tbody></table>
<p>Память: 65536 байт с побайтной адресацией, 16-разрядные слова хранятся младшим байтом вперёд. Адресация <code>[Rn]</code> охватывает первые 256 байт (нулевую страницу). Таблица векторов прерываний занимает <code>0xFFF0</code>..<code>0xFFFF</code>: восемь 16-разрядных адресов обработчиков, IRQ <em>n</em> по адресу <code>0xFFF0 + 2n</code>. Стек начинается сразу под таблицей.</p>
<p>Правила флагов: <code>ADD</code> устанавливает C при беззнаковом переносе и V при знаковом переполнении; <code>SUB</code> и <code>CMP</code> устанавливают C при заёме; логические команды сбрасывают C и V; сдвиги помещают выдвинутый бит в C; <code>INC</code> и <code>DEC</code> сохраняют C; <code>MOV</code>, <code>PUSH</code>, <code>POP</code> и <code>LOOP</code> флаги не изменяют. АЛУ никогда не изменяет I.</p>
<p>Сбои останавливают процессор с сообщением: недопустимый код операции, переполнение стека, чтение из пустого стека, деление на ноль, переполнение при делении (частное больше 255).</p>` },
    { h: 'Цикл команды и анимация', html: `
<p>Каждая команда проходит три фазы. <strong>Выборка</strong>: код операции по адресу IP читается в IR, IP увеличивается. <strong>Декодирование</strong>: код операции ищется в таблице команд, читаются байты операндов, текст команды появляется рядом с IR. <strong>Исполнение</strong>: операция выполняется на тракте данных.</p>
<p>Каждое обращение к памяти показано двумя пакетами. Жёлтый пакет идёт от MAR по адресной шине к ячейке памяти. Затем голубой пакет идёт по шине данных: от ячейки к MDR при чтении, от MDR к ячейке при записи. Ячейка вспыхивает голубым при чтении и жёлтым при записи. Регистр вспыхивает при изменении значения. АЛУ показывает операнды, операцию и результат; светодиоды флагов отражают результат.</p>
<p>Кроме команд существуют два вида шагов. <strong>IRQ</strong>: устройство управления подтверждает ожидающее прерывание, помещает в стек FLAGS и IP, сбрасывает I и переходит через таблицу векторов; журнал показывает номер линии и адрес обработчика. <strong>Ожидание</strong>: после <code>HLT</code> с установленным I процессор простаивает один квант за шаг до прихода прерывания.</p>
<p>Ползунок скорости задаёт паузу между микрооперациями. Крайнее правое положение отключает анимацию: движок выполняет сотни шагов за кадр, и панели обновляются в конце каждой порции.</p>` },
    { h: 'Как написать и запустить программу', html: `
<ol>
<li>Выберите <em>Своя программа</em> в списке <em>Программа</em> или возьмите пример и измените его. Любая правка помечает программу как изменённую.</li>
<li>Введите текст в панели <em>Исходный текст</em>. Клавиша Tab вставляет восемь пробелов.</li>
<li>Нажмите <em>Ассемблировать</em> (Ctrl+Enter). Листинг покажет адреса и байты; окно памяти покажет загруженный код синим и данные зелёным. При ошибке выводятся номер строки и причина, строка выделяется в редакторе.</li>
<li>Нажмите <em>Запуск</em> (F5) для непрерывного исполнения или <em>Шаг</em> (F8) для одного шага. Во время исполнения кнопка <em>Запуск</em> превращается в <em>Паузу</em>; клавиша Esc тоже ставит паузу.</li>
<li>Щелчок по адресу в листинге ставит точку останова: непрерывное исполнение останавливается перед этой командой каждый раз, когда IP доходит до неё.</li>
<li><em>Сброс</em> заново загружает программу: регистры, память, устройства и модель тайминга возвращаются в начальное состояние. Программа, выполнившая <code>HLT</code>, при следующем <em>Запуске</em> начинается сначала.</li>
<li>Программы, читающие устройство ввода, берут байты из текстового поля в панели <em>Устройства</em>. Содержимое поля ставится в очередь при каждой загрузке; кнопка <em>Отправить</em> добавляет текст во время работы программы.</li>
</ol>
<p>Минимальная программа:</p>
<pre>        MOV A, 40
        ADD A, 2
        OUT 0, A        ; печатает 42
        HLT</pre>` },
    { h: 'Язык ассемблера', html: `
<p>Одна инструкция в строке: <code>[метка:] МНЕМОНИКА [операнд[, операнд]] [; комментарий]</code>. Регистр букв в мнемониках, именах регистров и метках значения не имеет. Метка обозначает адрес следующей за ней инструкции и допустима везде, где ожидается число, до или после своего определения.</p>
<table><thead><tr><th>Операнд</th><th>Значение</th><th>Пример</th></tr></thead><tbody>
<tr><td><code>A</code>, <code>R0</code>..<code>R7</code></td><td>8-разрядные регистры</td><td><code>MOV A, R3</code></td></tr>
<tr><td><code>SI</code>, <code>DI</code>, <code>BP</code>, <code>SP</code></td><td>16-разрядные адресные регистры</td><td><code>MOV SI, buffer</code></td></tr>
<tr><td><code>FLAGS</code></td><td>регистр флагов (только PUSH и POP)</td><td><code>PUSH FLAGS</code></td></tr>
<tr><td>число, метка</td><td>непосредственное значение (8 или 16 разрядов) или адрес перехода</td><td><code>MOV A, 0x2A</code>, <code>JMP loop</code></td></tr>
<tr><td><code>[addr]</code></td><td>прямая адресация: байт (или слово) по 16-разрядному адресу</td><td><code>MOV A, [0x1234]</code></td></tr>
<tr><td><code>[Rn]</code></td><td>косвенная через нулевую страницу: байт по адресу Rn (0..255)</td><td><code>MOV [R0], A</code></td></tr>
<tr><td><code>[addr+Rn]</code></td><td>индексная: байт по адресу addr + Rn</td><td><code>MOV A, [table+R1]</code></td></tr>
<tr><td><code>[SI]</code>, <code>[DI]</code>, <code>[BP]</code>, <code>[SP]</code></td><td>косвенная регистровая: байт по адресу из регистра</td><td><code>ADD A, [SI]</code></td></tr>
<tr><td><code>[BP+disp]</code>, <code>[BP-disp]</code></td><td>базовая: байт по адресу регистр плюс знаковое смещение (-128..127)</td><td><code>MOV A, [BP+4]</code></td></tr>
</tbody></table>
<p>Числа: десятичные <code>42</code>, шестнадцатеричные <code>0x2A</code> или <code>$2A</code>, двоичные <code>0b101010</code>, символ <code>'A'</code>, отрицательные <code>-1</code> (дополнительный код). Слагаемые объединяются знаками <code>+</code> и <code>-</code>: <code>[table+2]</code>, <code>'z'+1</code>.</p>
<p>Директивы: <code>.org addr</code> задаёт счётчик размещения; <code>.db v, v, "text"</code> размещает байты; <code>.dw word, label</code> размещает 16-разрядные слова младшим байтом вперёд; <code>.ds count</code> резервирует обнулённые байты; <code>.equ NAME, value</code> определяет константу. Точка входа - первая команда в тексте, поэтому данные, размещённые перед кодом, пропускаются.</p>` },
    { h: 'Группы команд', html: `
<p><strong>Пересылки данных.</strong> <code>MOV</code> копирует байт или слово между регистрами, памятью и константами; разрядность определяется операндами (<code>SI</code>, <code>DI</code>, <code>BP</code>, <code>SP</code> и <code>imm16</code> 16-разрядные). <code>XCHG A, Rn</code> обменивает два регистра. <code>LEA XR, [XS+disp]</code> сохраняет сам вычисленный адрес. Флаги при пересылках сохраняются.</p>
<p><strong>Арифметика.</strong> <code>ADD</code>, <code>ADC</code> (с переносом), <code>SUB</code>, <code>SBB</code> (с заёмом) и <code>CMP</code> объединяют A с константой, регистром или байтом памяти; <code>CMP</code> изменяет только флаги. <code>INC</code>, <code>DEC</code> и <code>NEG</code> работают с одним операндом, в том числе прямо в памяти. <code>MUL Rn</code> образует 16-разрядное произведение <code>R7:A</code>; <code>DIV Rn</code> делит <code>R7:A</code> на Rn, оставляет частное в A и остаток в R7. <code>ADD</code>, <code>SUB</code>, <code>CMP</code>, <code>INC</code>, <code>DEC</code> с адресным регистром выполняются в 16 разрядах; <code>ADD XR, A</code> прибавляет аккумулятор к адресному регистру для индексации таблиц.</p>
<p><strong>Логика и сдвиги.</strong> <code>AND</code>, <code>OR</code>, <code>XOR</code>, <code>NOT</code> сбрасывают C и V. <code>SHL</code>, <code>SHR</code> сдвигают A на один бит и помещают выдвинутый бит в C; <code>ROL</code>, <code>ROR</code> вращают A и копируют перенесённый бит в C.</p>
<p><strong>Передача управления.</strong> <code>JMP addr</code> выполняет переход; <code>JZ</code>, <code>JNZ</code>, <code>JC</code>, <code>JNC</code>, <code>JS</code>, <code>JNS</code>, <code>JO</code>, <code>JNO</code> выполняют переход по флагу. <code>CALL addr</code> помещает 16-разрядный адрес возврата в стек и переходит; <code>RET</code> снимает его. <code>LOOP Rn, addr</code> уменьшает Rn и переходит, пока Rn остаётся больше нуля. <code>JMP [XR]</code> читает 16-разрядный адрес из памяти: основа таблиц переходов.</p>
<p><strong>Стек и кадры.</strong> <code>PUSH</code> уменьшает SP и записывает; <code>POP</code> читает и увеличивает. Адресные регистры помещаются в стек словами, A, Rn и FLAGS байтами. <code>ENTER n</code> помещает BP в стек, копирует SP в BP и резервирует n байт локальных переменных; <code>LEAVE</code> восстанавливает SP и BP. Внутри кадра <code>[BP+2]</code> содержит адрес возврата, <code>[BP+4]</code> первый аргумент, помещённый вызывающей стороной, <code>[BP-1]</code> первую локальную переменную.</p>
<p><strong>Строковые операции.</strong> <code>LODSB</code> загружает A из [SI], <code>STOSB</code> сохраняет A в [DI], <code>MOVSB</code> копирует [SI] в [DI], <code>CMPSB</code> сравнивает [SI] с [DI], <code>SCASB</code> сравнивает A с [DI]; индексные регистры увеличиваются на единицу. Формы <code>REP</code> повторяют операцию R6 раз; <code>REP CMPSB</code> останавливается на первом различии, <code>REP SCASB</code> на первом совпадении.</p>
<p><strong>Прерывания.</strong> <code>EI</code> и <code>DI</code> устанавливают и сбрасывают I. Ожидающий запрос на линии подтверждается между командами при установленном I. <code>INT n</code> вызывает вектор n из программы. <code>IRET</code> возвращает из обработчика. <code>HLT</code> при установленном I ждёт следующего прерывания; при сброшенном I останавливает процессор.</p>
<p><strong>Ввод и вывод.</strong> <code>OUT port, A</code> записывает A в порт, <code>IN A, port</code> читает порт в A. Порты без устройства возвращают <code>0xFF</code>.</p>
<p><strong>Флаги и NOP.</strong> <code>CLC</code> и <code>STC</code> сбрасывают и устанавливают C; <code>NOP</code> занимает такт и ничего не изменяет.</p>
<p>Кнопка <em>Система команд</em> открывает полную таблицу с кодами операций, синтаксисом и длиной в байтах.</p>` },
    { h: 'Эпохи микроархитектуры', html: `
<table><thead><tr><th>Эпоха</th><th>Модель</th><th>Содержимое блока</th></tr></thead><tbody>
<tr><td>1975</td><td>Многотактное устройство управления в стиле 8080: 4 такта на выборку кода операции, 3 на каждый следующий байт памяти, дополнительные такты на INC / DEC / сдвиги, 16 на MUL, 24 на DIV, 11 на подтверждение прерывания. Циклы шины идут один за другим.</td><td>Ряд циклов шины и счётчики.</td></tr>
<tr><td>1985</td><td>Блок шины в стиле 8086: очередь предвыборки на 6 байт, заполняемая 16-разрядными циклами шины во время работы исполнительного блока. Байт из очереди занимает 1 такт; при отсутствии байта исполнительный блок ждёт шину. Выполненный переход сбрасывает очередь. Обмен с памятью данных и портами занимает 4 такта, операция АЛУ 2, MUL 30, DIV 40, прерывание 40.</td><td>Очередь с байтами и диапазоном адресов, попадания, промахи, сбросы.</td></tr>
<tr><td>1995</td><td>Конвейер в стиле Pentium: одна простая команда за такт. Кэши команд и данных прямого отображения по 16 строк на 16 байт; промах задерживает конвейер на 8 тактов. Буфер адресов переходов на 16 записей с 2-битными счётчиками; ошибка предсказания стоит 3 такта. MUL занимает 10 тактов, DIV 40, обмен с портом без кэша 8, прерывание 20.</td><td>Оба кэша построчно (номер страницы кэшированной строки), счётчики предсказателя, попадания, промахи, предсказания.</td></tr>
</tbody></table>
<p>Эпоха действует на весь прогон; смена значения в списке <em>Эпоха</em> перезагружает программу. Счётчик тактов в устройстве управления и число тактов каждого шага в журнале соответствуют выбранной модели.</p>` },
    { h: 'Устройства и порты', html: `
<table><thead><tr><th>Порт</th><th><code>OUT port, A</code></th><th><code>IN A, port</code></th></tr></thead><tbody>
<tr><td>0</td><td>печать A десятичным числом</td><td>следующий байт очереди ввода</td></tr>
<tr><td>1</td><td>печать A символом</td><td>следующий символ очереди ввода, 0 при пустой очереди</td></tr>
<tr><td>2, 3</td><td>период таймера, младший и старший байт, в тактах</td><td>чтение заданного периода</td></tr>
<tr><td>4</td><td>управление таймером: бит 0 запускает таймер</td><td>1, пока таймер работает</td></tr>
<tr><td>5</td><td></td><td>счётчик срабатываний таймера, младший байт</td></tr>
<tr><td>6</td><td></td><td>1, когда в очереди ввода ждёт байт</td></tr>
</tbody></table>
<p>Таймер считает такты каждого шага и поднимает IRQ 0 по истечении каждого периода. Адрес обработчика берётся из таблицы векторов; программа размещает его директивами <code>.org 0xFFF0</code> и <code>.dw handler</code>. Пример <em>Прерывание от таймера</em> показывает полную схему.</p>
<p>Устройство ввода хранит очередь байтов. Текстовое поле в панели <em>Устройства</em> ставится в очередь при каждой загрузке программы; кнопка <em>Отправить</em> добавляет текст в любой момент. Пример <em>Эхо ввода</em> читает очередь, пока порт 6 сообщает о наличии байтов.</p>` },
    { h: 'Окно памяти', html: `
<p>Окно показывает одну страницу размером 256 байт; подписи строк содержат полные 16-разрядные адреса. Цвета ячеек: синие содержат код, зелёные содержат данные из <code>.db</code> / <code>.dw</code> / <code>.ds</code>, фиолетовые относятся к стеку (от SP до <code>0xFFEF</code>), голубые образуют таблицу векторов. Жёлтая рамка отмечает байты текущей команды, подчёркивание отмечает SP.</p>
<p>При включённом режиме <em>следить</em> окно переключается на страницу каждого адреса, к которому обращается процессор: запись в стек показывает страницу стека, следующая выборка возвращает страницу кода. При выключенном режиме страница меняется кнопками со стрелками и кнопками <em>IP</em> и <em>SP</em>. Подсказка на ячейке показывает адрес и значение в шестнадцатеричном, десятичном и символьном виде.</p>` },
    { h: 'Движки, клавиши, параметры адреса', html: `
<p>Движок JavaScript работает в браузере и обходится без сервера. Движок .NET появляется в списке <em>Движок</em>, когда страницу отдаёт <code>CISCProcessorWeb</code>; каждый шаг тогда выполняется запросом к серверу, формат трасс совпадает. Оба движка дают одинаковые результаты и одинаковое число тактов.</p>
<p>Клавиши: F5 запуск или пауза, F8 шаг, Ctrl+Enter ассемблирование, Esc пауза, Tab в редакторе вставляет пробелы.</p>
<p>Параметры адреса: <code>?program=sort</code> выбирает пример, <code>&amp;era=Pipelined1995</code> выбирает эпоху (<code>Classic1975</code>, <code>Prefetch1985</code>, <code>Pipelined1995</code>), <code>&amp;lang=ru</code> выбирает язык интерфейса. Язык, эпоха, скорость и тема запоминаются в браузере.</p>` },
  ];

  const dictionaries = { en, ru };
  let language = 'en';

  function t(key, params) {
    let text = dictionaries[language][key];
    if (text === undefined) text = en[key];
    if (text === undefined) return key;
    if (params) {
      for (const name of Object.keys(params)) {
        let value = params[name];
        if (typeof value === 'string' && value.startsWith('i18n:')) value = t(value.slice(5));
        text = text.split('{' + name + '}').join(String(value));
      }
    }
    return text;
  }

  global.CISC = global.CISC || {};
  global.CISC.I18N = {
    languages: ['en', 'ru'],
    get language() { return language; },
    set language(value) { language = dictionaries[value] ? value : 'en'; },
    t,
    statName: (name) => (language === 'ru' && statsRu[name]) || name,
    eventText: (op) => {
      if (language !== 'ru' || !eventsRu[op.name]) return op.text;
      const hex4 = (v) => (v & 0xFFFF).toString(16).toUpperCase().padStart(4, '0');
      return eventsRu[op.name].replace('{addr}', hex4(op.address)).replace('{line}', String(Math.floor((op.address < 0 ? 0 : op.address) / 16) % 16)).replace('{value}', String(op.value));
    },
    sampleName: (sample) => (language === 'ru' && samplesRu[sample.id]) ? samplesRu[sample.id][0] : sample.name,
    sampleDescription: (sample) => (language === 'ru' && samplesRu[sample.id]) ? samplesRu[sample.id][1] : sample.description,
    instructionDescription: (instruction) => (language === 'ru' && instructionsRu[instruction.opcode]) || instruction.description,
    guide: () => (language === 'ru' ? guideRu : guideEn),
  };
})(typeof window !== 'undefined' ? window : globalThis);
