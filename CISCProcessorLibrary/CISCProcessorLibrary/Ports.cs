namespace CISCProcessorLibrary;

/// <summary>Well known I/O port numbers.</summary>
public static class Ports
{
    /// <summary>OUT: the value is shown as an unsigned decimal number. IN: next byte of numeric input.</summary>
    public const byte Numeric = 0;

    /// <summary>OUT: the value is shown as an ASCII character. IN: next character of the input queue.</summary>
    public const byte Character = 1;

    /// <summary>Timer period, low byte (in clock cycles).</summary>
    public const byte TimerPeriodLow = 2;

    /// <summary>Timer period, high byte.</summary>
    public const byte TimerPeriodHigh = 3;

    /// <summary>Timer control: bit 0 enables the timer. Reading returns the same bits.</summary>
    public const byte TimerControl = 4;

    /// <summary>Timer tick counter (low byte), read only.</summary>
    public const byte TimerTicks = 5;

    /// <summary>Input status, read only: bit 0 = a byte is waiting in the input queue.</summary>
    public const byte InputStatus = 6;

    /// <summary>Value returned when nothing drives the bus (an unused port).</summary>
    public const byte FloatingBus = 0xFF;
}

/// <summary>Payload of <see cref="CISCProcessor.OutputWritten"/>.</summary>
public sealed class OutputEventArgs : EventArgs
{
    public OutputEventArgs(byte port, byte value)
    {
        Port = port;
        Value = value;
    }

    public byte Port { get; }

    public byte Value { get; }

    /// <summary>The value rendered the way a terminal attached to the port would show it.</summary>
    public string Text => Port == Ports.Character ? ((char)Value).ToString() : Value.ToString();
}

/// <summary>
/// The I/O address space: 256 ports that devices can claim. Reading an unclaimed port returns the
/// floating bus value 0xFF, writing to one goes nowhere - just like a real bus with nothing on it.
/// </summary>
public sealed class PortBus
{
    private readonly Func<byte>?[] readers = new Func<byte>?[256];
    private readonly Action<byte>?[] writers = new Action<byte>?[256];

    /// <summary>Lets a device answer reads of <paramref name="port"/>.</summary>
    public void MapInput(byte port, Func<byte> read) => readers[port] = read;

    /// <summary>Lets a device receive writes to <paramref name="port"/>.</summary>
    public void MapOutput(byte port, Action<byte> write) => writers[port] = write;

    /// <summary>True when a device answers reads of the port.</summary>
    public bool HasInput(byte port) => readers[port] is not null;

    /// <summary>True when a device receives writes to the port.</summary>
    public bool HasOutput(byte port) => writers[port] is not null;

    /// <summary>Reads a port; unclaimed ports return <see cref="Ports.FloatingBus"/>.</summary>
    public byte Read(byte port) => readers[port]?.Invoke() ?? Ports.FloatingBus;

    /// <summary>Writes a port; returns false when no device listens.</summary>
    public bool Write(byte port, byte value)
    {
        Action<byte>? writer = writers[port];
        if (writer is null) return false;
        writer(value);
        return true;
    }
}
