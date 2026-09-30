using System.Diagnostics;
using SSP.ExplorerKit.Internal;

namespace SSP.ExplorerKit;

/// <summary>Windows 11 File Explorer: open folders as a new tab, list and close Explorer windows.</summary>
/// <remarks>COM calls are executed on an STA thread; if the calling thread is not STA, a temporary STA thread is used.</remarks>
public static class Explorer
{
    /// <summary>
    /// Opens <paramref name="path"/> in a new Explorer tab and asks the user where, if necessary:
    /// <list type="bullet">
    /// <item>Explorer not running or no Explorer window open: Explorer is started with the path in a new window.</item>
    /// <item>One Explorer window open: new tab in this window.</item>
    /// <item>Several Explorer windows open: a dialog lets the user choose the window (or close windows that are no longer needed).</item>
    /// </list>
    /// </summary>
    /// <param name="path">Directory to open. Environment variables are expanded; for a file, its directory is opened.</param>
    /// <returns>How the path was opened; <see cref="OpenMode.Cancelled"/> if the user cancelled the dialog.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or invalid.</exception>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public static OpenResult OpenInteractive(string path) => Open(path, TargetWindow.Interactive);

    /// <summary>
    /// Opens <paramref name="path"/> in a new tab of the Explorer window selected by <paramref name="target"/>.
    /// If Explorer is not running or no Explorer window is open, Explorer is started with the path in a new window.
    /// </summary>
    /// <param name="path">Directory to open. Environment variables are expanded; for a file, its directory is opened.</param>
    /// <param name="target">Target window; defaults to <see cref="TargetWindow.MostRecent"/> (no dialog).</param>
    /// <param name="fallbackToNewWindow">Open a new window if the tab cannot be created; otherwise throw.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or invalid.</exception>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    /// <exception cref="ExplorerWindowNotFoundException">An explicitly requested window does not exist.</exception>
    /// <exception cref="InvalidOperationException">The tab could not be created and <paramref name="fallbackToNewWindow"/> is <c>false</c>.</exception>
    public static OpenResult Open(string path, TargetWindow? target = null, bool fallbackToNewWindow = true)
    {
        string directory = NormalizePath(path);
        target ??= TargetWindow.MostRecent;

        return StaThread.Run(() =>
        {
            ExplorerWindow? window;
            try
            {
                window = target.Select(WindowEnumerator.GetAll(), directory);
            }
            catch (OperationCanceledException)
            {
                return new OpenResult(directory, OpenMode.Cancelled, null, null);
            }

            if (window is null)
            {
                StartExplorer(directory);
                return new OpenResult(directory, OpenMode.NewWindow, null, null);
            }

            string reason;
            try
            {
                if (TabOpener.TryOpen(window.Handle, directory))
                {
                    return new OpenResult(directory, OpenMode.NewTab, window, null);
                }
                reason = "Could not create a new tab.";
            }
            catch (Exception ex) when (fallbackToNewWindow)
            {
                reason = $"Opening a new tab failed: {ex.Message}";
            }

            if (!fallbackToNewWindow)
            {
                throw new InvalidOperationException(reason);
            }

            StartExplorer(directory);
            return new OpenResult(directory, OpenMode.NewWindow, window, reason);
        });
    }

    /// <summary>Returns all visible Explorer windows, most recently used first.</summary>
    public static IReadOnlyList<ExplorerWindow> GetWindows() => StaThread.Run(WindowEnumerator.GetAll);

    /// <summary>Closes an Explorer window including all its tabs, like clicking its close button.</summary>
    /// <returns><c>true</c> if the window was closed; <c>false</c> if it is still open after <paramref name="timeout"/> (default 5 s).</returns>
    /// <exception cref="ExplorerWindowNotFoundException"><paramref name="window"/> is not an open Explorer window.</exception>
    public static bool CloseWindow(ExplorerWindow window, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        return CloseWindow(window.Handle, timeout);
    }

    /// <inheritdoc cref="CloseWindow(ExplorerWindow, TimeSpan?)"/>
    /// <param name="handle">Top-level handle of the Explorer window.</param>
    /// <param name="timeout">Maximum time to wait for the window to close (default 5 s).</param>
    public static bool CloseWindow(nint handle, TimeSpan? timeout = null)
    {
        // Guards against closing an unrelated window if the handle has been reused.
        if (!WindowEnumerator.IsExplorerWindow(handle))
        {
            throw new ExplorerWindowNotFoundException($"No Explorer window with handle 0x{handle:X} found.");
        }

        NativeMethods.PostMessage(handle, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

        var stopwatch = Stopwatch.StartNew();
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(5);
        while (NativeMethods.IsWindow(handle))
        {
            if (stopwatch.Elapsed >= limit)
            {
                return false;
            }
            Thread.Sleep(50);
        }
        return true;
    }

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
        if (File.Exists(fullPath))
        {
            fullPath = Path.GetDirectoryName(fullPath)!;
        }

        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
    }

    /// <summary>
    /// Starts explorer.exe with the directory. This opens a new window in the running Explorer
    /// and also starts Explorer (including the shell) if it is not running.
    /// </summary>
    private static void StartExplorer(string directory) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true })?.Dispose();
}
