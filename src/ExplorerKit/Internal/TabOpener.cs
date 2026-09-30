using System.Diagnostics;
using static SSP.ExplorerKit.Internal.NativeMethods;

namespace SSP.ExplorerKit.Internal;

/// <summary>
/// Opens a path in a new tab of an existing Windows 11 Explorer window.
/// Windows offers no public API for Explorer tabs, so this uses the same approach as
/// the tab shortcut (Ctrl+T): the undocumented WM_COMMAND 0xA21B sent to the active tab,
/// followed by navigating the newly created tab through the Shell.Application COM API.
/// </summary>
internal static class TabOpener
{
    private const string TabWindowClass = "ShellTabWindowClass";
    private const int CMD_NEW_TAB = 0xA21B;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <returns><c>false</c> if no tab could be created in the given Explorer window.</returns>
    public static bool TryOpen(IntPtr explorerWindow, string path)
    {
        // The first ShellTabWindowClass child is the active tab.
        IntPtr activeTab = FindWindowEx(explorerWindow, IntPtr.Zero, TabWindowClass, null);
        if (activeTab == IntPtr.Zero)
        {
            return false;
        }

        HashSet<IntPtr> existingTabs = GetTabHandles(explorerWindow);

        if (IsIconic(explorerWindow))
        {
            ShowWindow(explorerWindow, SW_RESTORE);
        }
        SetForegroundWindow(explorerWindow);

        SendMessage(activeTab, WM_COMMAND, CMD_NEW_TAB, IntPtr.Zero);

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < Timeout)
        {
            IntPtr newTab = GetTabHandles(explorerWindow).FirstOrDefault(h => !existingTabs.Contains(h));
            if (newTab != IntPtr.Zero)
            {
                dynamic? browser = FindBrowserForTab(newTab);
                if (browser is not null)
                {
                    browser.Navigate2(path);
                    return true;
                }
            }
            Thread.Sleep(50);
        }

        return false;
    }

    private static HashSet<IntPtr> GetTabHandles(IntPtr explorerWindow)
    {
        var tabs = new HashSet<IntPtr>();
        IntPtr child = IntPtr.Zero;
        while ((child = FindWindowEx(explorerWindow, child, TabWindowClass, null)) != IntPtr.Zero)
        {
            tabs.Add(child);
        }
        return tabs;
    }

    private static object? FindBrowserForTab(IntPtr tabHandle) =>
        WindowEnumerator.GetShellBrowsers()
            .FirstOrDefault(b => WindowEnumerator.TryGetTabHandle(b, out IntPtr hwnd) && hwnd == tabHandle);
}
