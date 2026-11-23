using Avalonia.Threading;
using MouseEnhance.Models;
using MouseEnhance.Services.Features;
using Serilog;

namespace MouseEnhance.Services;

internal sealed class FindMouseController(FindMouseSpotlight spotlight, ILogger logger)
{
    private readonly FindMouseGestures _gestures = new();
    private readonly object _sync = new();
    private FindMouseSettings _settings = new();
    private bool _running;
    private bool _showRequested;
    private long _presentationVersion;

    public void Start(FindMouseSettings settings)
    {
        lock (_sync)
        {
            _settings = settings;
            _gestures.Reset();
            var now = Environment.TickCount64;

            // The shared hooks only report transitions after registration.
            for (var key = 0x08; key <= 0xfe; key++)
            {
                if (key is not (0x10 or 0x11 or 0x12) && GrabWindowNative.IsKeyDown(key))
                    _gestures.KeyDown(key, now, enabled: false);
            }

            _gestures.SetMouseButton(0, GrabWindowNative.IsKeyDown(0x01));
            _gestures.SetMouseButton(1, GrabWindowNative.IsKeyDown(0x02));
            _gestures.SetMouseButton(2, GrabWindowNative.IsKeyDown(0x04));
            _gestures.SetMouseButton(3, GrabWindowNative.IsKeyDown(0x05));
            _gestures.SetMouseButton(4, GrabWindowNative.IsKeyDown(0x06));
            _running = true;
        }
    }

    public async Task StopAsync()
    {
        long version;
        lock (_sync)
        {
            _running = false;
            _showRequested = false;
            version = ++_presentationVersion;
            _gestures.Reset();
        }

        if (Dispatcher.UIThread.CheckAccess())
            HideIfCurrent(version);
        else
            await Dispatcher.UIThread.InvokeAsync(() => HideIfCurrent(version));
    }

    public void UpdateSettings(FindMouseSettings settings)
    {
        lock (_sync)
        {
            if (_settings.Enabled != settings.Enabled ||
                _settings.DoubleCtrlEnabled != settings.DoubleCtrlEnabled ||
                _settings.ShakeEnabled != settings.ShakeEnabled ||
                _settings.ShakeThreshold != settings.ShakeThreshold)
                _gestures.ResetSequences();

            if (_settings.Enabled && !settings.Enabled)
                DismissLocked();

            _settings = settings;
        }
    }

    public async Task<FindMouseSettings> PreviewAsync()
    {
        long version;
        lock (_sync)
        {
            if (!_running)
                throw new InvalidOperationException("鼠标增强插件尚未启动。");

            _showRequested = true;
            version = ++_presentationVersion;
        }

        return await Dispatcher.UIThread.InvokeAsync(() => ShowIfCurrent(version));
    }

    public void ObserveKey(KeyboardEventArgs args, bool down)
    {
        if (args.IsSimulator)
            return;

        var key = (int)args.CurrentKey;
        var now = Environment.TickCount64;
        lock (_sync)
        {
            if (!_running)
                return;

            var enabled = _settings.Enabled && _settings.DoubleCtrlEnabled;
            if (down)
            {
                _gestures.KeyDown(key, now, enabled);
                if (key == 0x1b)
                    DismissLocked();
            }
            else if (_gestures.KeyUp(key, now, enabled))
            {
                RequestShowLocked();
            }
        }
    }

    public void ObserveButton(MouseEventArgs args, bool down)
    {
        if (args.IsSimulator)
            return;

        var button = args.CurrentKey switch
        {
            MouseKey.Left => 0,
            MouseKey.Right => 1,
            MouseKey.Middle => 2,
            MouseKey.XButton1 => 3,
            MouseKey.XButton2 => 4,
            _ => -1
        };
        if (button < 0)
            return;

        lock (_sync)
        {
            if (!_running)
                return;

            _gestures.SetMouseButton(button, down);
            if (down)
                DismissLocked();
        }
    }

    public void ObserveMove(MouseEventArgs args)
    {
        if (args.IsSimulator)
            return;

        lock (_sync)
        {
            if (!_running || !_settings.Enabled || !_settings.ShakeEnabled)
                return;

            var point = args.Position;
            if (_gestures.MouseMove((int)point.X, (int)point.Y, Environment.TickCount64,
                    _settings.ShakeThreshold, enabled: true))
                RequestShowLocked();
        }
    }

    public void ResetShake() => _gestures.ResetShake();

    private void RequestShowLocked()
    {
        _showRequested = true;
        var version = ++_presentationVersion;
        Dispatcher.UIThread.Post(() =>
        {
            try { ShowIfCurrent(version); }
            catch (Exception error) { logger.Warning(error, "Could not show Find Mouse spotlight"); }
        });
    }

    private void DismissLocked()
    {
        if (!_showRequested)
            return;

        _showRequested = false;
        var version = ++_presentationVersion;
        Dispatcher.UIThread.Post(() => HideIfCurrent(version));
    }

    private FindMouseSettings ShowIfCurrent(long version)
    {
        FindMouseSettings settings;
        lock (_sync)
        {
            settings = _settings;
            if (!_running || version != _presentationVersion)
                return settings;
        }

        spotlight.Show(settings);
        return settings;
    }

    private void HideIfCurrent(long version)
    {
        lock (_sync)
        {
            if (version != _presentationVersion)
                return;
        }

        spotlight.Hide();
    }
}
