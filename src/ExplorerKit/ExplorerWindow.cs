namespace SSP.ExplorerKit;

/// <summary>An open File Explorer window.</summary>
/// <param name="Index">1-based position in Z-order (1 = most recently used). Changes when another window is activated.</param>
/// <param name="Handle">Top-level window handle. Stable as long as the window is open.</param>
/// <param name="Title">Window title.</param>
/// <param name="Tabs">The window's tabs.</param>
/// <param name="OpenedAt">When the window was opened (start time of its UI thread), or <c>null</c> if unknown.</param>
public sealed record ExplorerWindow(int Index, nint Handle, string Title, IReadOnlyList<ExplorerTab> Tabs, DateTimeOffset? OpenedAt)
{
    /// <summary>How long the window has been open, or <c>null</c> if unknown.</summary>
    public TimeSpan? Age => OpenedAt is { } openedAt ? DateTimeOffset.Now - openedAt : null;
}

/// <summary>A tab of an Explorer window.</summary>
/// <param name="Name">Display name as shown on the tab, e.g. "Downloads" or "Dieser PC".</param>
/// <param name="Location">File system path, or the display name for virtual locations such as "Dieser PC".</param>
public sealed record ExplorerTab(string Name, string Location);
