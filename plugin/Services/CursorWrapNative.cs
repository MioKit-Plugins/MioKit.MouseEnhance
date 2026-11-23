using System.Runtime.InteropServices;
using MouseEnhance.Models;
using MouseEnhance.Services.Features;

namespace MouseEnhance.Services;

internal static class CursorWrapNative
{
    internal static readonly nuint MoveTag = 0x43575250; // CWRP

    private static readonly nint PerMonitorAwareV2 = -4;
    private const int AbsoluteCoordinateRange = 65536;
    private const uint MouseInputType = 0;
    private const uint Move = 0x0001;
    private const uint VirtualDesktop = 0x4000;
    private const uint Absolute = 0x8000;

    public static CursorWrapMonitor[] ReadMonitors()
    {
        var previousContext = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        if (previousContext == 0)
            return [];

        try
        {
            return ReadMonitorBounds();
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousContext);
        }
    }

    public static bool TryMove(
        ScreenPoint previous,
        ScreenPoint current,
        ScreenPoint target,
        IReadOnlyList<CursorWrapMonitor> monitors)
    {
        if (!GetPhysicalCursorPos(out var cursor))
            return false;

        var actual = new ScreenPoint(cursor.X, cursor.Y);
        var stillAtSource = actual == current || actual == previous;
        var targetOnDisplay = false;
        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = int.MinValue;
        var bottom = int.MinValue;
        foreach (var monitor in monitors)
        {
            left = Math.Min(left, monitor.Left);
            top = Math.Min(top, monitor.Top);
            right = Math.Max(right, monitor.Right);
            bottom = Math.Max(bottom, monitor.Bottom);
            targetOnDisplay |= monitor.Contains(target);

            if (!stillAtSource && monitor.Contains(previous))
            {
                // The hook can report an attempted position outside the connected displays.
                stillAtSource = actual.X == Math.Clamp(current.X, monitor.Left, monitor.Right - 1) &&
                                actual.Y == Math.Clamp(current.Y, monitor.Top, monitor.Bottom - 1);
            }
        }

        if (!stillAtSource || !targetOnDisplay ||
            !TryNormalize(target.X, left, right, out var x) || !TryNormalize(target.Y, top, bottom, out var y))
        {
            return false;
        }

        var previousContext = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        if (previousContext == 0)
            return false;

        try
        {
            // Hotplug changes the virtual coordinate frame even if the destination still exists.
            var currentMonitors = ReadMonitorBounds();
            if (currentMonitors.Length != monitors.Count || currentMonitors.Any(monitor => !monitors.Contains(monitor)) ||
                !GetClipCursor(out var clip) ||
                target.X < clip.Left || target.X >= clip.Right || target.Y < clip.Top || target.Y >= clip.Bottom)
            {
                return false;
            }

            var input = new NativeInput
            {
                Type = MouseInputType,
                Mouse = new NativeMouseInput
                {
                    X = x,
                    Y = y,
                    Flags = Move | VirtualDesktop | Absolute,
                    ExtraInfo = MoveTag
                }
            };
            return SendInput(1, in input, Marshal.SizeOf<NativeInput>()) == 1;
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousContext);
        }
    }

    private static CursorWrapMonitor[] ReadMonitorBounds()
    {
        var monitors = new List<CursorWrapMonitor>();
        var success = EnumDisplayMonitors(0, 0, (nint _, nint _, ref NativeRect rect, nint _) =>
        {
            var monitor = new CursorWrapMonitor(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (monitor.Left < monitor.Right && monitor.Top < monitor.Bottom && !monitors.Contains(monitor))
                monitors.Add(monitor);
            return true;
        }, 0);

        return success ? monitors.ToArray() : [];
    }

    private static bool TryNormalize(int coordinate, int start, int end, out int normalized)
    {
        var size = (long)end - start;
        var offset = (long)coordinate - start;
        normalized = 0;
        if (size <= 0 || offset < 0 || offset >= size)
            return false;

        // The first normalized value that maps to the desired physical pixel. On a desktop
        // larger than the normalized range some pixels cannot be targeted exactly.
        var scaled = (offset * AbsoluteCoordinateRange + size - 1) / size;
        if (scaled >= AbsoluteCoordinateRange || scaled * size / AbsoluteCoordinateRange != offset)
            return false;

        normalized = (int)scaled;
        return true;
    }

    private delegate bool MonitorEnumProc(nint monitor, nint deviceContext, ref NativeRect rect, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public NativeMouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint dpiContext);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        nint deviceContext, nint clip, MonitorEnumProc callback, nint data);

    [DllImport("user32.dll")]
    private static extern bool GetPhysicalCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool GetClipCursor(out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, in NativeInput input, int size);
}
