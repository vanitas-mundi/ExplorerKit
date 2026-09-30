using System.Runtime.InteropServices;
using System.Text;
using static SSP.ExplorerKit.Internal.NativeMethods;

namespace SSP.ExplorerKit.Internal;

/// <summary>Finds Explorer windows and their tabs.</summary>
internal static class WindowEnumerator
{
    private const string ExplorerWindowClass = "CabinetWClass";

    private static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");

    /// <summary>All visible Explorer windows, most recently used first.</summary>
    public static List<ExplorerWindow> GetAll()
    {
        var handles = new List<IntPtr>();
        EnumWindows((hWnd, _) =>
        {
            if (IsWindowVisible(hWnd) && GetClassName(hWnd) == ExplorerWindowClass)
            {
                handles.Add(hWnd);
            }
            return true;
        }, IntPtr.Zero);

        var tabsByWindow = new Dictionary<IntPtr, List<ExplorerTab>>();
        foreach (object browser in GetShellBrowsers())
        {
            try
            {
                var hwnd = new IntPtr(Convert.ToInt64(((dynamic)browser).HWND));
                if (!tabsByWindow.TryGetValue(hwnd, out List<ExplorerTab>? tabs))
                {
                    tabsByWindow[hwnd] = tabs = [];
                }
                tabs.Add(GetTab(browser));
            }
            catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // Not an Explorer window or closed while enumerating.
            }
        }

        return handles
            .Select((h, i) => new ExplorerWindow(
                i + 1,
                h,
                GetWindowText(h),
                tabsByWindow.TryGetValue(h, out List<ExplorerTab>? tabs) ? tabs : [],
                GetOpenedAt(h)))
            .ToList();
    }

    /// <summary>Entries of Shell.Application.Windows() (one IWebBrowser2 per Explorer tab).</summary>
    public static IEnumerable<object> GetShellBrowsers()
    {
        Type shellType = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new InvalidOperationException("Shell.Application is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic windows = shell.Windows();

        int count = windows.Count;
        for (int i = 0; i < count; i++)
        {
            object? window;
            try
            {
                window = windows.Item(i);
            }
            catch (COMException)
            {
                continue;
            }

            if (window is not null)
            {
                yield return window;
            }
        }
    }

    /// <summary>Returns the ShellTabWindowClass handle of the tab hosting the given browser.</summary>
    public static bool TryGetTabHandle(object browser, out IntPtr tabHandle)
    {
        tabHandle = IntPtr.Zero;
        try
        {
            if (browser is not IComServiceProvider serviceProvider)
            {
                return false;
            }

            Guid sid = SID_STopLevelBrowser;
            Guid iid = IID_IShellBrowser;
            return serviceProvider.QueryService(ref sid, ref iid, out object shellBrowserObj) == 0
                && shellBrowserObj is IShellBrowser shellBrowser
                && shellBrowser.GetWindow(out tabHandle) == 0;
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// <summary>Whether <paramref name="hWnd"/> is an existing top-level Explorer window.</summary>
    public static bool IsExplorerWindow(IntPtr hWnd) =>
        hWnd != IntPtr.Zero && IsWindow(hWnd) && GetClassName(hWnd) == ExplorerWindowClass;

    /// <summary>
    /// Explorer runs every window on its own UI thread, so the creation time of that thread
    /// is the time the window was opened.
    /// </summary>
    private static DateTimeOffset? GetOpenedAt(IntPtr hWnd)
    {
        uint threadId = GetWindowThreadProcessId(hWnd, out _);
        if (threadId == 0)
        {
            return null;
        }

        IntPtr thread = OpenThread(THREAD_QUERY_LIMITED_INFORMATION, false, threadId);
        if (thread == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return GetThreadTimes(thread, out long creationTime, out _, out _, out _)
                ? DateTimeOffset.FromFileTime(creationTime)
                : null;
        }
        finally
        {
            CloseHandle(thread);
        }
    }

    private static ExplorerTab GetTab(dynamic browser)
    {
        string name = string.Empty;
        try
        {
            name = (string)browser.LocationName ?? string.Empty;
        }
        catch (Exception)
        {
            // Keep empty name.
        }

        string? path = null;
        try
        {
            path = (string)browser.Document.Folder.Self.Path;
        }
        catch (Exception)
        {
            // Virtual locations (e.g. Home) may not expose a folder.
        }

        // Virtual folders have parsing names like "::{GUID}" - show the display name instead.
        bool isFileSystemPath = !string.IsNullOrEmpty(path) && !path.StartsWith("::", StringComparison.Ordinal);
        return new ExplorerTab(name, isFileSystemPath ? path! : name);
    }

    private static string GetClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        return NativeMethods.GetClassName(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    private static string GetWindowText(IntPtr hWnd)
    {
        var sb = new StringBuilder(512);
        return NativeMethods.GetWindowText(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }
}
