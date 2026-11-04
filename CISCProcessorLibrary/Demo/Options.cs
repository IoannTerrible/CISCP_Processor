using CISCProcessorLibrary.Timing;

namespace CISCProcessorDemo;

/// <summary>Command line options of the demo.</summary>
internal sealed class Options
{
    public string? FilePath { get; private set; }

    public string? SampleId { get; private set; }

    public bool Trace { get; set; }

    public bool MicroOps { get; set; }

    public int DelayMilliseconds { get; set; }

    public int MaxSteps { get; private set; } = 1_000_000;

    public bool NoColor { get; private set; }

    public bool ListSamples { get; private set; }

    public bool ShowHelp { get; private set; }

    public Microarchitecture Microarchitecture { get; set; } = Microarchitecture.Classic1975;

    /// <summary>Text queued into the input device before the program starts.</summary>
    public string? Input { get; set; }

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
                    options.MaxSteps = ParseInt(args, ref i, arg);
                    break;
                case "--sample":
                    options.SampleId = Next(args, ref i, arg);
                    break;
                case "--arch":
                    options.Microarchitecture = ParseArchitecture(Next(args, ref i, arg));
                    break;
                case "--input":
                    options.Input = Next(args, ref i, arg);
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

    /// <summary>Accepts the year, the name or the enum member: 1975 / classic, 1985 / prefetch, 1995 / pipelined.</summary>
    public static Microarchitecture ParseArchitecture(string text) => text.ToLowerInvariant() switch
    {
        "1975" or "classic" or "classic1975" or "8080" => Microarchitecture.Classic1975,
        "1985" or "prefetch" or "prefetch1985" or "8086" => Microarchitecture.Prefetch1985,
        "1995" or "pipelined" or "pipeline" or "pipelined1995" or "pentium" => Microarchitecture.Pipelined1995,
        _ => throw new ArgumentException($"Unknown micro-architecture '{text}'. Use 1975 (classic), 1985 (prefetch) or 1995 (pipelined)."),
    };

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
