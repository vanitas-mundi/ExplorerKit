using SSP.ExplorerKit.UI;

namespace SSP.ExplorerKit;

/// <summary>Determines in which open Explorer window <see cref="Explorer.Open"/> creates the new tab.</summary>
public sealed class TargetWindow
{
    private readonly Func<IReadOnlyList<ExplorerWindow>, string, ExplorerWindow?> _selector;
    private readonly string? _notFoundMessage;

    private TargetWindow(Func<IReadOnlyList<ExplorerWindow>, string, ExplorerWindow?> selector, string? notFoundMessage)
    {
        _selector = selector;
        _notFoundMessage = notFoundMessage;
    }

    /// <summary>The most recently used Explorer window. Opens a new window if none is open. Default of <see cref="Explorer.Open"/>.</summary>
    public static TargetWindow MostRecent { get; } = new((windows, _) => windows.FirstOrDefault(), null);

    /// <summary>
    /// If several Explorer windows are open, a dialog lets the user choose the window
    /// (and close windows that are no longer needed); otherwise behaves like <see cref="MostRecent"/>.
    /// Cancelling the dialog results in <see cref="OpenMode.Cancelled"/>.
    /// </summary>
    public static TargetWindow Interactive { get; } = new(
        (windows, path) => windows.Count <= 1 ? windows.FirstOrDefault() : WindowPickerDialog.Choose(windows, path),
        null);

    /// <summary>The window with the given 1-based <see cref="ExplorerWindow.Index"/> (1 = most recently used).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 1.</exception>
    public static TargetWindow Index(int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 1);
        return new((windows, _) => windows.FirstOrDefault(w => w.Index == index),
            $"Explorer window #{index} not found.");
    }

    /// <summary>The window with the given top-level window handle.</summary>
    public static TargetWindow Handle(nint handle) =>
        new((windows, _) => windows.FirstOrDefault(w => w.Handle == handle),
            $"No Explorer window with handle 0x{handle:X} found.");

    /// <summary>The most recently used window whose title or one of its tabs contains <paramref name="text"/> (case-insensitive).</summary>
    public static TargetWindow Match(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new((windows, _) => windows.FirstOrDefault(w =>
                w.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || w.Tabs.Any(t => t.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                                   || t.Location.Contains(text, StringComparison.OrdinalIgnoreCase))),
            $"No Explorer window matching \"{text}\" found.");
    }

    /// <summary>
    /// A custom selection. The selector receives all open Explorer windows (most recently used first).
    /// Returning <c>null</c> opens the path in a new window; throwing <see cref="OperationCanceledException"/>
    /// results in <see cref="OpenMode.Cancelled"/>.
    /// </summary>
    public static TargetWindow Custom(Func<IReadOnlyList<ExplorerWindow>, ExplorerWindow?> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new((windows, _) => selector(windows), null);
    }

    /// <param name="windows">All open Explorer windows.</param>
    /// <param name="path">The directory to be opened (shown in the dialog).</param>
    /// <exception cref="ExplorerWindowNotFoundException">An explicitly requested window does not exist.</exception>
    /// <exception cref="OperationCanceledException">The user cancelled the selection.</exception>
    internal ExplorerWindow? Select(IReadOnlyList<ExplorerWindow> windows, string path)
    {
        ExplorerWindow? window = _selector(windows, path);
        if (window is null && _notFoundMessage is not null)
        {
            throw new ExplorerWindowNotFoundException($"{_notFoundMessage} ({windows.Count} open)");
        }
        return window;
    }
}
