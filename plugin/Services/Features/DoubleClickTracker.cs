using System.Runtime.InteropServices;
using MouseEnhance.Models;

namespace MouseEnhance.Services.Features;

/// <summary>Detects left-button double clicks for low-level mouse hooks.</summary>
internal sealed class DoubleClickTracker
{
    private ScreenPoint _lastPoint;
    private long _lastTick;
    private bool _hasLast;

    public bool TryRegisterLeftDown(ScreenPoint point)
    {
        var now = Environment.TickCount64;
        var timeLimit = GetDoubleClickTime();
        GetDoubleClickDistance(out var dxLimit, out var dyLimit);

        if (_hasLast &&
            now - _lastTick <= timeLimit &&
            Math.Abs(point.X - _lastPoint.X) <= dxLimit &&
            Math.Abs(point.Y - _lastPoint.Y) <= dyLimit)
        {
            _hasLast = false;
            _lastTick = 0;
            return true;
        }

        _hasLast = true;
        _lastPoint = point;
        _lastTick = now;
        return false;
    }

    public void Reset()
    {
        _hasLast = false;
        _lastTick = 0;
    }

    private static void GetDoubleClickDistance(out int dx, out int dy)
    {
        dx = Math.Max(1, GetSystemMetrics(36)); // SM_CXDOUBLECLK
        dy = Math.Max(1, GetSystemMetrics(37)); // SM_CYDOUBLECLK
    }

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
