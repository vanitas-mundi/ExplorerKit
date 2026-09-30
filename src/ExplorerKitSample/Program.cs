using SSP.ExplorerKit;

namespace SSP.ExplorerKitSample;

internal static class Program
{
    private enum ExitCode
    {
        Ok = 0,
        InvalidArguments = 1,
        DirectoryNotFound = 2,
        WindowNotFound = 3,
        Cancelled = 4,
    }

    [STAThread]
    private static int Main(string[] args) => (int)Run(args);

    private static ExitCode Run(string[] args)
    {
        CommandLine? commandLine = CommandLine.Parse(args, out string? error);
        if (commandLine is null)
        {
            if (error is not null)
            {
                Console.Error.WriteLine(error);
            }
            Console.Error.WriteLine(CommandLine.Usage);
            return ExitCode.InvalidArguments;
        }

        if (commandLine.List)
        {
            PrintWindows(Explorer.GetWindows());
            return ExitCode.Ok;
        }

        try
        {
            OpenResult result = commandLine.Target is null
                ? Explorer.OpenInteractive(commandLine.Path!)
                : Explorer.Open(commandLine.Path!, commandLine.Target);

            if (result.FallbackReason is not null)
            {
                Console.Error.WriteLine($"{result.FallbackReason} Opened in a new window instead.");
            }
            return result.Mode == OpenMode.Cancelled ? ExitCode.Cancelled : ExitCode.Ok;
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.DirectoryNotFound;
        }
        catch (ExplorerWindowNotFoundException ex)
        {
            Console.Error.WriteLine($"{ex.Message} Use --list.");
            return ExitCode.WindowNotFound;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"Invalid path: {ex.Message}");
            return ExitCode.InvalidArguments;
        }
    }

    private static void PrintWindows(IReadOnlyList<ExplorerWindow> windows)
    {
        if (windows.Count == 0)
        {
            Console.WriteLine("No Explorer window open.");
            return;
        }

        foreach (ExplorerWindow window in windows)
        {
            Console.WriteLine($"[{window.Index}] 0x{window.Handle:X}  {window.Title}");
            Console.WriteLine($"      Geöffnet: {ExplorerFormat.OpenedAt(window.OpenedAt)} (läuft seit {ExplorerFormat.Age(window.Age)})");
            foreach (ExplorerTab tab in window.Tabs)
            {
                Console.WriteLine($"      - {tab.Location}");
            }
        }
    }
}
