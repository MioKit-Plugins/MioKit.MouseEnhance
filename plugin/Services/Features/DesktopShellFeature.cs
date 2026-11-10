using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using MouseEnhance.Models;

namespace MouseEnhance.Services.Features;

/// <summary>
/// Desktop icon toggle and taskbar show-desktop.
/// <para>
/// Hook path only uses WindowFromPoint + class walks; accessibility hit-test and
/// toggle run deferred off the hook thread. Never send LVM_HITTEST with local
/// memory to Explorer: WM_USER messages do not marshal pointers across processes,
/// even when called outside the hook. Explorer can crash dereferencing that pointer.
/// </para>
/// Icon toggle: Explorer command <c>WM_COMMAND 0x7402</c> → SHELLDLL_DefView
/// (m417z / common C# samples = "View → Show desktop icons").
/// </summary>
internal static class DesktopShellFeature
{
    private const uint WmCommand = 0x0111;
    private const nint ToggleDesktopIconsCommand = 0x7402;
    private const int HookDeferMs = 80;

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    /// <summary>
    /// Hook-safe: cursor is over the desktop surface.
    /// Only WindowFromPoint / GetClassName / GetParent / FindWindowEx — no SendMessage / EnumThreadWindows.
    /// </summary>
    public static bool IsDesktopSurface(ScreenPoint point)
    {
        var hwnd = WindowFromPoint(new NativePoint(point.X, point.Y));
        while (hwnd != 0)
        {
            var className = GetClassName(hwnd);
            if (className == "SysListView32" && GetClassName(GetParent(hwnd)) == "SHELLDLL_DefView")
                return true;
            if (className is "SHELLDLL_DefView" or "Progman")
                return true;
            if (className == "WorkerW" && FindWindowEx(hwnd, 0, "SHELLDLL_DefView", null) != 0)
                return true;
            hwnd = GetParent(hwnd);
        }

        return false;
    }

    /// <summary>Hook-safe: cursor is over the taskbar (primary or secondary).</summary>
    public static bool IsTaskbarArea(ScreenPoint point)
    {
        var hwnd = WindowFromPoint(new NativePoint(point.X, point.Y));
        while (hwnd != 0)
        {
            var className = GetClassName(hwnd);
            if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
                return true;
            hwnd = GetParent(hwnd);
        }

        return false;
    }

    /// <summary>
    /// Schedule icon toggle after the LL-hook returns. Does not suppress the click
    /// (caller should return false) so Explorer can finish input normally; we only
    /// toggle when the click was on empty desktop.
    /// </summary>
    public static void ScheduleToggleDesktopIconsIfEmpty(ScreenPoint point)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(HookDeferMs).ConfigureAwait(false);
                if (!TryGetEmptyDesktopView(point, out var defView))
                    return;

                PostMessage(defView, WmCommand, ToggleDesktopIconsCommand, 0);
            }
            catch
            {
                // Ignore; gesture already finished.
            }
        });
    }

    /// <summary>Schedule Show Desktop after the LL-hook returns.</summary>
    public static void ScheduleToggleShowDesktop()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(HookDeferMs).ConfigureAwait(false);
                QueueOnSta(static () =>
                {
                    var type = Type.GetTypeFromProgID("Shell.Application")
                               ?? throw new InvalidOperationException("Shell.Application ProgID missing.");
                    dynamic shell = Activator.CreateInstance(type)!;
                    shell.ToggleDesktop();
                });
            }
            catch
            {
                // Ignore.
            }
        });
    }

    /// <summary>Deferred only: MSAA handles cross-process marshaling for the hit test.</summary>
    private static bool TryGetEmptyDesktopView(ScreenPoint point, out nint defView)
    {
        defView = 0;
        // A window may have opened over the desktop during the delay.
        if (!IsDesktopSurface(point))
            return false;

        var hwnd = WindowFromPoint(new NativePoint(point.X, point.Y));
        var listView = ResolveDesktopListView(hwnd);
        if (listView == 0)
            return false;

        // Explorer hides the list-view window when desktop icons are hidden.
        // Its invisible items must not prevent the next gesture from showing them.
        if (IsWindowVisible(listView) && !DesktopIconHitTest.IsEmpty(listView, point))
            return false;

        defView = GetParent(listView);
        return GetClassName(defView) == "SHELLDLL_DefView";
    }

    private static nint GetDesktopShellDefView()
    {
        var progman = FindWindow("Progman", "Program Manager");
        if (progman == 0)
            progman = FindWindow("Progman", null);
        if (progman == 0)
            return 0;

        var defView = FindWindowEx(progman, 0, "SHELLDLL_DefView", null);
        if (defView != 0)
            return defView;

        var threadId = GetWindowThreadProcessId(progman, out _);
        nint found = 0;
        EnumThreadWindows(threadId, (hWnd, _) =>
        {
            if (GetClassName(hWnd) != "WorkerW")
                return true;

            var child = FindWindowEx(hWnd, 0, "SHELLDLL_DefView", null);
            if (child == 0)
                return true;

            found = child;
            return false;
        }, 0);

        if (found != 0)
            return found;

        nint worker = 0;
        while ((worker = FindWindowEx(0, worker, "WorkerW", null)) != 0)
        {
            defView = FindWindowEx(worker, 0, "SHELLDLL_DefView", null);
            if (defView != 0)
                return defView;
        }

        return 0;
    }

    private static void QueueOnSta(System.Action action)
    {
        void SafeRun()
        {
            try
            {
                action();
            }
            catch
            {
                // Ignore shell failures.
            }
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                SafeRun();
                return;
            }

            Dispatcher.UIThread.Post(SafeRun);
            return;
        }
        catch
        {
            // Fall through.
        }

        var thread = new Thread(SafeRun);
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    /// <summary>Used only from deferred (non-hook) path.</summary>
    private static nint ResolveDesktopListView(nint hwnd)
    {
        while (hwnd != 0)
        {
            var className = GetClassName(hwnd);
            if (className == "SysListView32")
            {
                var parent = GetParent(hwnd);
                if (GetClassName(parent) == "SHELLDLL_DefView")
                    return hwnd;
            }

            if (className == "SHELLDLL_DefView")
                return FindWindowEx(hwnd, 0, "SysListView32", "FolderView");

            if (className is "Progman" or "WorkerW")
            {
                var defView = GetDesktopShellDefView();
                return defView == 0 ? 0 : FindWindowEx(defView, 0, "SysListView32", "FolderView");
            }

            hwnd = GetParent(hwnd);
        }

        return 0;
    }

    private static string GetClassName(nint hwnd)
    {
        var sb = new StringBuilder(256);
        _ = GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern nint GetParent(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpfn, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
