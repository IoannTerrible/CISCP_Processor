namespace CISCProcessorLibrary.Devices;

/// <summary>
/// A programmable interval timer on IRQ 0, the device every 1970s single board computer had.
/// The program sets the period in clock cycles through ports 2 and 3 and starts it through port 4;
/// every time the period elapses the timer raises IRQ 0.
/// </summary>
public sealed class TimerDevice : IDevice
{
    /// <summary>Interrupt request line the timer is wired to.</summary>
    public const int Irq = 0;

    private readonly CISCProcessor cpu;
    private long counter;

    public TimerDevice(CISCProcessor cpu)
    {
        this.cpu = cpu;
        PortBus ports = cpu.Ports;
        ports.MapOutput(Ports.TimerPeriodLow, v => Period = (ushort)((Period & 0xFF00) | v));
        ports.MapOutput(Ports.TimerPeriodHigh, v => Period = (ushort)((Period & 0x00FF) | (v << 8)));
        ports.MapOutput(Ports.TimerControl, v => Enabled = (v & 1) != 0);
        ports.MapInput(Ports.TimerPeriodLow, () => (byte)Period);
        ports.MapInput(Ports.TimerPeriodHigh, () => (byte)(Period >> 8));
        ports.MapInput(Ports.TimerControl, () => (byte)(Enabled ? 1 : 0));
        ports.MapInput(Ports.TimerTicks, () => (byte)Ticks);
    }

    public string Name => "Programmable interval timer";

    /// <summary>Clock cycles between two interrupts. Zero disables the timer.</summary>
    public ushort Period { get; set; }

    /// <summary>Whether the timer is running.</summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            if (!value) counter = 0;
        }
    }

    private bool enabled;

    /// <summary>Number of interrupts raised since reset.</summary>
    public long Ticks { get; private set; }

    /// <summary>Cycles elapsed since the last tick.</summary>
    public long Elapsed => counter;

    public void Reset()
    {
        Period = 0;
        enabled = false;
        counter = 0;
        Ticks = 0;
    }

    public void Tick(int cycles)
    {
        if (!enabled || Period == 0) return;
        counter += cycles;
        while (counter >= Period)
        {
            counter -= Period;
            Ticks++;
            cpu.RequestInterrupt(Irq);
        }
    }
}
