using MouseEnhance.Models;

namespace MouseEnhance.Services.Features;

internal readonly record struct CursorWrapMonitor(int Left, int Top, int Right, int Bottom)
{
    public bool Contains(ScreenPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
}

internal static class CursorWrapGeometry
{
    public static ScreenPoint ClampToDesktop(ReadOnlySpan<CursorWrapMonitor> monitors,
        ScreenPoint? previous, ScreenPoint current)
    {
        foreach (var monitor in monitors)
        {
            if (monitor.Contains(current))
                return current;
        }

        if (previous is { } from)
        {
            foreach (var monitor in monitors)
            {
                if (monitor.Contains(from))
                {
                    return new ScreenPoint(Math.Clamp(current.X, monitor.Left, monitor.Right - 1),
                        Math.Clamp(current.Y, monitor.Top, monitor.Bottom - 1));
                }
            }
        }

        return current;
    }

    public static bool TryGetTarget(
        ReadOnlySpan<CursorWrapMonitor> monitors,
        ScreenPoint previous,
        ScreenPoint current,
        CursorWrapMode mode,
        out ScreenPoint target)
    {
        target = default;
        if (current == previous)
            return false;

        CursorWrapMonitor source = default;
        foreach (var monitor in monitors)
        {
            if (!monitor.Contains(previous))
                continue;

            source = monitor;
            break;
        }

        if (source == default)
            return false;

        // A move already on another display is an ordinary crossing. For an attempted move
        // outside all displays, use the last display to interpret the direction of motion.
        if (!source.Contains(current))
        {
            foreach (var monitor in monitors)
            {
                if (monitor.Contains(current))
                    return false;
            }
        }

        var dx = (long)current.X - previous.X;
        var dy = (long)current.Y - previous.Y;
        var horizontal = mode is CursorWrapMode.Both or CursorWrapMode.Horizontal;
        var vertical = mode is CursorWrapMode.Both or CursorWrapMode.Vertical;

        if (horizontal && (!vertical || Math.Abs(dx) >= Math.Abs(dy)))
        {
            return TryGetAxisTarget(monitors, source, current, dx, horizontal: true, out target) ||
                   (vertical && TryGetAxisTarget(monitors, source, current, dy, horizontal: false, out target));
        }

        return (vertical && TryGetAxisTarget(monitors, source, current, dy, horizontal: false, out target)) ||
               (horizontal && TryGetAxisTarget(monitors, source, current, dx, horizontal: true, out target));
    }

    private static bool TryGetAxisTarget(
        ReadOnlySpan<CursorWrapMonitor> monitors,
        CursorWrapMonitor source,
        ScreenPoint current,
        long delta,
        bool horizontal,
        out ScreenPoint target)
    {
        target = default;
        if (delta == 0)
            return false;

        var axis = horizontal ? current.X : current.Y;
        var across = horizontal ? current.Y : current.X;
        var start = horizontal ? source.Left : source.Top;
        var end = horizontal ? source.Right : source.Bottom;
        var increasing = delta > 0;
        var edge = increasing ? end - 1 : start;
        if (increasing ? axis < edge : axis > edge)
            return false;

        // At a diagonal overshoot, use the last reachable row or column of the source display.
        var acrossStart = horizontal ? source.Top : source.Left;
        var acrossEnd = horizontal ? source.Bottom : source.Right;
        across = Math.Clamp(across, acrossStart, acrossEnd - 1);

        var outsideEdge = increasing ? (long)edge + 1 : (long)edge - 1;
        var opposite = increasing ? start : end - 1;
        foreach (var monitor in monitors)
        {
            var monitorAcrossStart = horizontal ? monitor.Top : monitor.Left;
            var monitorAcrossEnd = horizontal ? monitor.Bottom : monitor.Right;
            var monitorStart = horizontal ? monitor.Left : monitor.Top;
            var monitorEnd = horizontal ? monitor.Right : monitor.Bottom;
            if (across < monitorAcrossStart || across >= monitorAcrossEnd || monitorStart >= monitorEnd)
                continue;

            // An adjacent display at this specific row or column owns the next physical pixel.
            if (outsideEdge >= monitorStart && outsideEdge < monitorEnd)
                return false;

            opposite = increasing ? Math.Min(opposite, monitorStart) : Math.Max(opposite, monitorEnd - 1);
        }

        if (edge == opposite)
            return false;

        target = horizontal ? new ScreenPoint(opposite, across) : new ScreenPoint(across, opposite);
        return true;
    }
}
