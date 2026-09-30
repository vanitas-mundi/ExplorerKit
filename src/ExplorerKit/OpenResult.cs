namespace SSP.ExplorerKit;

/// <summary>How a path was opened.</summary>
public enum OpenMode
{
    /// <summary>Opened in a new tab of an existing Explorer window.</summary>
    NewTab,

    /// <summary>Opened in a new Explorer window.</summary>
    NewWindow,

    /// <summary>Nothing was opened because the user cancelled the window selection.</summary>
    Cancelled,
}

/// <summary>Result of <see cref="Explorer.Open"/> and <see cref="Explorer.OpenInteractive"/>.</summary>
/// <param name="Path">The directory that was (or would have been) opened.</param>
/// <param name="Mode">Whether a new tab or a new window was used, or the selection was cancelled.</param>
/// <param name="Window">The selected target window, or <c>null</c> if none was open or selected.</param>
/// <param name="FallbackReason">Why a new window was opened although a target window was selected; otherwise <c>null</c>.</param>
public sealed record OpenResult(string Path, OpenMode Mode, ExplorerWindow? Window, string? FallbackReason);
