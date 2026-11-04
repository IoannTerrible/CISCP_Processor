using System.Text;

namespace CISCProcessorLibrary.Devices;

/// <summary>
/// A simple input device (think of a terminal keyboard or a paper tape reader) behind ports 0, 1 and 6.
/// IN A, 1 returns the next queued character (0 when the queue is empty), IN A, 0 returns the next byte
/// as a number, and IN A, 6 reports whether anything is waiting. When the queue runs dry
/// <see cref="OnEmpty"/> can supply more input interactively.
/// </summary>
public sealed class InputDevice : IDevice
{
    private readonly Queue<byte> queue = new();

    public InputDevice(CISCProcessor cpu)
    {
        PortBus ports = cpu.Ports;
        ports.MapInput(Ports.Numeric, ReadByte);
        ports.MapInput(Ports.Character, ReadByte);
        ports.MapInput(Ports.InputStatus, () => (byte)(Available > 0 ? 1 : 0));
    }

    public string Name => "Input device";

    /// <summary>Called when the program reads from an empty queue; may return text to enqueue, or null.</summary>
    public Func<string?>? OnEmpty { get; set; }

    /// <summary>Bytes waiting to be read.</summary>
    public int Available => queue.Count;

    /// <summary>Queues the characters of <paramref name="text"/> (ASCII, high characters are clipped to 8 bits).</summary>
    public void Enqueue(string text)
    {
        foreach (char c in text)
            queue.Enqueue((byte)c);
    }

    /// <summary>Queues a single byte.</summary>
    public void Enqueue(byte value) => queue.Enqueue(value);

    /// <summary>Removes everything from the queue.</summary>
    public void Clear() => queue.Clear();

    /// <summary>The queued bytes rendered as text.</summary>
    public string PeekText()
    {
        var text = new StringBuilder();
        foreach (byte b in queue) text.Append((char)b);
        return text.ToString();
    }

    public void Reset() => queue.Clear();

    public void Tick(int cycles)
    {
    }

    private byte ReadByte()
    {
        if (queue.Count == 0 && OnEmpty is not null)
        {
            string? more = OnEmpty();
            if (!string.IsNullOrEmpty(more)) Enqueue(more);
        }
        return queue.Count > 0 ? queue.Dequeue() : (byte)0;
    }
}
