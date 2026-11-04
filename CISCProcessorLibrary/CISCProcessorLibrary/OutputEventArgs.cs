namespace CISCProcessorLibrary;

/// <summary>Well known output ports used by the OUT instruction.</summary>
public static class Ports
{
    /// <summary>Port 0: the value is shown as an unsigned decimal number.</summary>
    public const byte Numeric = 0;

    /// <summary>Port 1: the value is shown as an ASCII character.</summary>
    public const byte Character = 1;
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
