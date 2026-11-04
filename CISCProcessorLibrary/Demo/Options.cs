namespace CISCProcessorDemo;

/// <summary>Command line options of the demo.</summary>
internal sealed class Options
{
    public string? FilePath { get; private set; }

    public string? SampleId { get; private set; }

    public bool Trace { get; set; }

    public bool MicroOps { get; set; }

    public int DelayMilliseconds { get; set; }

    public int MaxInstructions { get; private set; } = 100_000;

    public bool NoColor { get; private set; }

    public bool ListSamples { get; private set; }

    public bool ShowHelp { get; private set; }

    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--trace":
                    options.Trace = true;
                    break;
                case "--micro":
                    options.Trace = true;
                    options.MicroOps = true;
                    break;
                case "--delay":
                    options.DelayMilliseconds = ParseInt(args, ref i, arg);
                    options.Trace = true;
                    break;
                case "--max":
                    options.MaxInstructions = ParseInt(args, ref i, arg);
                    break;
                case "--sample":
                    options.SampleId = Next(args, ref i, arg);
                    break;
                case "--no-color":
                    options.NoColor = true;
                    break;
                case "--list":
                    options.ListSamples = true;
                    break;
                case "--help" or "-h" or "/?":
                    options.ShowHelp = true;
                    break;
                default:
                    if (arg.StartsWith('-'))
                        throw new ArgumentException($"Unknown option '{arg}'.");
                    if (options.FilePath is not null)
                        throw new ArgumentException("Only one program file can be given.");
                    options.FilePath = arg;
                    break;
            }
        }
        return options;
    }

    private static string Next(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Option '{option}' needs a value.");
        return args[++i];
    }

    private static int ParseInt(string[] args, ref int i, string option)
    {
        string value = Next(args, ref i, option);
        if (!int.TryParse(value, out int number) || number < 0)
            throw new ArgumentException($"Option '{option}' needs a non-negative number, got '{value}'.");
        return number;
    }
}
