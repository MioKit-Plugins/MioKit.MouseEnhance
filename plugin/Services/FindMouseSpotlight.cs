using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using MouseEnhance.Models;
using MouseEnhance.Views;

namespace MouseEnhance.Services;

/// <summary>Displays and owns the temporary spotlight windows on the Avalonia UI thread.</summary>
public sealed class FindMouseSpotlight(Action<Exception>? reportError = null)
{
    private readonly List<FindMouseSpotlightWindow> _windows = [];
    private DispatcherTimer? _timer;
    private Screens? _screens;
    private long _startedAt;
    private Appearance _appearance;

    public void Show(FindMouseSettings settings)
    {
        Dispatcher.UIThread.VerifyAccess();

        try
        {
            var appearance = new Appearance(
                Color.Parse(settings.SpotlightColor), settings.SpotlightDiameter, settings.MaskOpacity,
                settings.GlowOpacity, settings.AnimationDurationMs, settings.HoldDurationMs);
            if (_timer is not null && _appearance == appearance)
            {
                var now = Stopwatch.GetTimestamp();
                var elapsedMs = Stopwatch.GetElapsedTime(_startedAt, now).TotalMilliseconds;
                if (elapsedMs >= appearance.AnimationDurationMs)
                {
                    // Resume the entrance at the current radius and opacity if the spotlight is fading out.
                    var entranceMs = Math.Clamp(appearance.TotalDurationMs - elapsedMs, 0, appearance.AnimationDurationMs);
                    _startedAt = now - (long)(entranceMs * Stopwatch.Frequency / 1000d);
                    AdvanceFrame(now);
                }

                return;
            }

            Hide();
            _appearance = appearance;
            var first = CreateWindow();
            _screens = first.Screens;
            var displays = _screens.All;
            if (displays.Count == 0)
            {
                Hide();
                return;
            }

            first.ShowOnDisplay(displays[0]);
            for (var index = 1; index < displays.Count; index++)
                CreateWindow().ShowOnDisplay(displays[index]);

            _screens.Changed += OnScreensChanged;
            _startedAt = Stopwatch.GetTimestamp();

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(1000d / 60)
            };
            _timer.Tick += OnTick;
            _timer.Start();
            AdvanceFrame(_startedAt);
        }
        catch
        {
            Hide();
            throw;
        }
    }

    public void Hide()
    {
        Dispatcher.UIThread.VerifyAccess();

        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }

        if (_screens is not null)
        {
            _screens.Changed -= OnScreensChanged;
            _screens = null;
        }

        foreach (var window in _windows)
        {
            window.Closed -= OnWindowClosed;
            window.Close();
        }

        _windows.Clear();
    }

    private FindMouseSpotlightWindow CreateWindow()
    {
        var window = new FindMouseSpotlightWindow(
            _appearance.Color, _appearance.MaskOpacity / 100d, _appearance.GlowOpacity / 100d);
        _windows.Add(window);
        window.Closed += OnWindowClosed;
        WindowManager.SetOwnerId(window, MouseEnhanceConst.PluginId);
        return window;
    }

    private void OnScreensChanged(object? sender, EventArgs args) => Hide();

    private void OnWindowClosed(object? sender, EventArgs args)
    {
        if (sender is FindMouseSpotlightWindow window && _windows.Remove(window))
        {
            window.Closed -= OnWindowClosed;
            Hide();
        }
    }

    private void OnTick(object? sender, EventArgs args)
    {
        try
        {
            AdvanceFrame(Stopwatch.GetTimestamp());
        }
        catch (Exception exception)
        {
            Hide();
            reportError?.Invoke(exception);
        }
    }

    private void AdvanceFrame(long now)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(_startedAt, now).TotalMilliseconds;
        var remainingMs = _appearance.TotalDurationMs - elapsedMs;
        if (remainingMs <= 0 || !FindMouseSpotlightNative.TryGetPhysicalCursorPosition(out var cursor))
        {
            Hide();
            return;
        }

        var animationMs = _appearance.AnimationDurationMs;
        var progress = animationMs == 0 ? 1 : Math.Min(1, Math.Min(elapsedMs, remainingMs) / animationMs);
        var focus = progress * progress * (3 - 2 * progress);
        var radiusScale = 1 + 2 * (1 - focus);

        var cursorScale = _screens!.ScreenFromPoint(cursor)?.Scaling ?? _screens.Primary?.Scaling ?? 1;
        var physicalRadius = _appearance.Diameter / 2d * cursorScale * radiusScale;
        foreach (var window in _windows)
            window.SetFrame(cursor, physicalRadius, focus);
    }

    private readonly record struct Appearance(
        Color Color, int Diameter, int MaskOpacity, int GlowOpacity, int AnimationDurationMs, int HoldDurationMs)
    {
        public int TotalDurationMs => 2 * AnimationDurationMs + HoldDurationMs;
    }
}
