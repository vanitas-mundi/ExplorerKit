using System.Globalization;
using SSP.ExplorerKit;

namespace SSP.ExplorerKitSample;

/// <summary>Parsed command line.</summary>
/// <param name="Path">Directory to open; <c>null</c> for <c>--list</c>.</param>
/// <param name="List">List the open Explorer windows instead of opening a path.</param>
/// <param name="Target">Target window; <c>null</c> = default (dialog if several windows are open).</param>
internal sealed record CommandLine(string? Path, bool List, TargetWindow? Target)
{
    public const string Usage = """
        Usage:
          OpenInExplorer <path> [target]
          OpenInExplorer --list

        Target (optional):
          (none)             One Explorer window open: new tab there.
                             Several open: a dialog lets you choose the window.
          --no-dialog        Don't show the dialog, use the most recently used window
          --window <n>       Explorer window number n as shown by --list (1 = most recently used)
          --hwnd <handle>    Explorer window handle (decimal or 0x hex) as shown by --list
          --match <text>     First window whose title or one of its tabs contains <text>

        If no Explorer window is open, the path is opened in a new window.
        """;

    /// <returns><c>null</c> if the arguments are invalid or help was requested; <paramref name="error"/> then describes the problem (if any).</returns>
    public static CommandLine? Parse(string[] args, out string? error)
    {
        error = null;
        string? path = null;
        bool list = false;
        TargetWindow? target = null;
        int targetOptions = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "-h" or "--help" or "/?":
                    return null;

                case "--list":
                    list = true;
                    break;

                case "--no-dialog":
                    target = TargetWindow.MostRecent;
                    targetOptions++;
                    break;

                case "--window" or "--hwnd" or "--match":
                    if (i + 1 >= args.Length)
                    {
                        error = $"Missing value for {arg}.";
                        return null;
                    }
                    target = ParseTarget(arg.ToLowerInvariant(), args[++i], out error);
                    if (target is null)
                    {
                        return null;
                    }
                    targetOptions++;
                    break;

                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"Unknown option: {arg}";
                        return null;
                    }
                    if (path is not null)
                    {
                        error = "Only one path may be specified.";
                        return null;
                    }
                    path = arg;
                    break;
            }
        }

        if (targetOptions > 1)
        {
            error = "Only one of --window, --hwnd, --match, --no-dialog may be specified.";
            return null;
        }

        if (!list && string.IsNullOrWhiteSpace(path))
        {
            error = "No path specified.";
            return null;
        }

        return new CommandLine(path, list, target);
    }

    private static TargetWindow? ParseTarget(string option, string value, out string? error)
    {
        error = null;
        switch (option)
        {
            case "--match":
                return TargetWindow.Match(value);

            case "--window" when int.TryParse(value, out int index) && index >= 1:
                return TargetWindow.Index(index);

            case "--hwnd" when TryParseHandle(value, out nint handle):
                return TargetWindow.Handle(handle);

            default:
                error = option == "--window" ? $"Invalid window number: {value}" : $"Invalid window handle: {value}";
                return null;
        }
    }

    private static bool TryParseHandle(string value, out nint handle)
    {
        bool ok = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long raw)
            : long.TryParse(value, out raw);
        handle = ok ? (nint)raw : 0;
        return ok && raw != 0;
    }
}
