namespace CISCProcessorLibrary;

/// <summary>An error found while assembling source text.</summary>
public sealed class AssemblyException : Exception
{
    public AssemblyException(int line, string message)
        : base($"Line {line}: {message}")
    {
        Line = line;
        Detail = message;
    }

    /// <summary>1-based source line the error belongs to.</summary>
    public int Line { get; }

    /// <summary>The message without the line prefix.</summary>
    public string Detail { get; }
}
