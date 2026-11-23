using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;

namespace MouseEnhance.Services;

internal static class FindMouseSpotlightNative
{
    private const uint WsDisabled = 0x08000000;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint WmNcHitTest = 0x0084;
    private const uint WmMouseActivate = 0x0021;
    private const int HtTransparent = -1;
    private const int MaNoActivate = 3;

    // Avalonia's Win32 window class uses CS_OWNDC. Keep its compositor and avoid WS_EX_LAYERED.
    // WS_DISABLED also excludes the overlay from WindowFromPoint in the other mouse enhancements.
    public static (uint WindowStyle, uint ExtendedWindowStyle) AddOverlayStyles(uint style, uint extendedStyle)
        => (style | WsDisabled, extendedStyle | WsExTransparent | WsExToolWindow | WsExNoActivate);

    public static nint HandleOverlayHitTest(nint hwnd, uint message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case WmNcHitTest:
                handled = true;
                return HtTransparent;
            case WmMouseActivate:
                handled = true;
                return MaNoActivate;
            default:
                return 0;
        }
    }

    public static void FitToScreen(nint handle, PixelRect bounds)
    {
        if (!SetWindowPos(handle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static bool TryGetPhysicalCursorPosition(out PixelPoint point)
    {
        var success = GetPhysicalCursorPos(out var cursor);
        point = new PixelPoint(cursor.X, cursor.Y);
        return success;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalCursorPos(out NativePoint point);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint handle, nint insertAfter, int x, int y, int width, int height, uint flags);
}
