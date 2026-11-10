using MouseEnhance.Models;
using MouseEnhance.Services.Features;
using Serilog;

namespace MouseEnhance.Services;

/// <summary>
/// Suite orchestrator: window drag, volume scroll, desktop / taskbar double-click.
/// Registers on shared <see cref="IMouseHook"/> / <see cref="IKeyboardHook"/> (does not own them).
/// </summary>
internal sealed class MouseEnhanceService :
    IPluginLifecycleComponent,
    IMouseInputHandler,
    IKeyboardInputHandler
{
    private const InputEventHandleResult Consumed =
        InputEventHandleResult.Handled | InputEventHandleResult.SuppressEvent;

    private readonly MouseEnhancePlugin _plugin;
    private readonly WindowDragCore _windowDrag;
    private readonly IMouseHook _mouseHook;
    private readonly IKeyboardHook _keyboardHook;
    private readonly ILogger _logger;
    private readonly VolumeScrollFeature _volumeScroll = new();
    private readonly DoubleClickTracker _doubleClick = new();
    private readonly object _sync = new();

    private bool _windowDragEnabled;
    private bool _volumeScrollEnabled;
    private bool _desktopIconsEnabled;
    private bool _taskbarShowDesktopEnabled;
    private MouseEnhanceModifier _activationModifier = MouseEnhanceModifier.Alt;
    private bool _handlersRegistered;

    public MouseEnhanceService(
        MouseEnhancePlugin plugin,
        WindowDragCore windowDrag,
        IMouseHook mouseHook,
        IKeyboardHook keyboardHook,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(windowDrag);
        ArgumentNullException.ThrowIfNull(mouseHook);
        ArgumentNullException.ThrowIfNull(keyboardHook);
        ArgumentNullException.ThrowIfNull(logger);
        _plugin = plugin;
        _windowDrag = windowDrag;
        _mouseHook = mouseHook;
        _keyboardHook = keyboardHook;
        _logger = logger;
    }

    public string InputHandlerId => MouseEnhanceConst.InputHandlerId;
    public ushort InputHandlerOrder => MouseEnhanceConst.InputHandlerOrder;
    public bool IsEnableHandler { get; set; } = true;

    public bool SupportKeyDown => true;
    public bool SupportKeyUp => true;
    public bool SupportUp => true;
    public bool SupportDown => true;
    public bool SupportMove => true;
    public bool SupportWhere => false;
    public bool SupportDoubleClick => false;

    public bool IsWindowDragEnabled
    {
        get
        {
            lock (_sync)
                return _windowDragEnabled;
        }
    }

    public void EnableWindowDrag(bool persist = true)
    {
        lock (_sync)
            _windowDragEnabled = true;

        if (persist)
            _plugin.SetWindowDragEnabled(true);

        ReloadSettingsFromPlugin();
        _logger.Information("Window drag enabled");
    }

    public void DisableWindowDrag(bool persist = true)
    {
        lock (_sync)
            _windowDragEnabled = false;

        _windowDrag.Reset();

        if (persist)
            _plugin.SetWindowDragEnabled(false);

        ReloadSettingsFromPlugin();
        _logger.Information("Window drag disabled");
    }

    public void ReloadSettingsFromPlugin()
    {
        var windowDragOn = _plugin.GetWindowDragEnabled() != false;
        var volumeOn = _plugin.GetVolumeScrollEnabled() != false;
        var desktopOn = _plugin.GetDesktopIconsToggleEnabled() == true;
        var taskbarOn = _plugin.GetTaskbarShowDesktopEnabled() == true;
        var modifier = _plugin.GetActivationModifier() ?? MouseEnhanceModifier.Alt;

        lock (_sync)
        {
            _windowDragEnabled = windowDragOn;
            _volumeScrollEnabled = volumeOn;
            _desktopIconsEnabled = desktopOn;
            _taskbarShowDesktopEnabled = taskbarOn;
            _activationModifier = modifier;
        }

        var settings = new WindowDragSettings
        {
            Enabled = windowDragOn,
            ModifierKey = modifier,
            ShouldAbsorbAlt = _plugin.GetPreventAltWindowMenu() != false,
            UseRightClickResize = _plugin.GetRightClickResizeEnabled() != false,
            ExcludedApps = ParseExcludedLines(_plugin.GetExcludedApps()),
            ModifierExcludedApps = ParseExcludedLines(_plugin.GetModifierExcludedApps())
        };

        _windowDrag.UpdateSettings(settings);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _keyboardHook.StartAsync(cancellationToken).ConfigureAwait(false);
        await _mouseHook.StartAsync(cancellationToken).ConfigureAwait(false);

        ReloadSettingsFromPlugin();

        _keyboardHook.RegisterHandler(this);
        _mouseHook.RegisterHandler(this);
        _handlersRegistered = true;

        _logger.Information(
            "MouseEnhance handlers registered (drag={Drag}, volume={Volume}, desktop={Desktop}, taskbar={Taskbar})",
            IsWindowDragEnabled,
            _plugin.GetVolumeScrollEnabled() != false,
            _plugin.GetDesktopIconsToggleEnabled() == true,
            _plugin.GetTaskbarShowDesktopEnabled() == true);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_handlersRegistered)
        {
            _mouseHook.UnregisterHandler(this);
            _keyboardHook.UnregisterHandler(this);
            _handlersRegistered = false;
        }

        _windowDrag.Reset();
        _doubleClick.Reset();
        _logger.Information("MouseEnhance handlers unregistered");
        return Task.CompletedTask;
    }

    public InputEventHandleResult HandleKeyDown(KeyboardEventArgs args)
    {
        if (args.IsSimulator || !IsWindowDragEnabled)
            return InputEventHandleResult.None;

        var vk = (uint)(int)args.CurrentKey;
        var disposition = _windowDrag.HandleKeyDown(vk, MapScanCode(vk), IsExtended(vk), args.IsSimulator);
        return ToResult(disposition);
    }

    public InputEventHandleResult HandleKeyUp(KeyboardEventArgs args)
    {
        if (args.IsSimulator || !IsWindowDragEnabled)
            return InputEventHandleResult.None;

        var disposition = _windowDrag.HandleKeyUp((uint)(int)args.CurrentKey, args.IsSimulator);
        return ToResult(disposition);
    }

    public InputEventHandleResult HandleDown(MouseEventArgs args)
    {
        var button = ToButton(args.CurrentKey);
        var point = new ScreenPoint((int)args.Position.X, (int)args.Position.Y);

        if (button == MouseButtonKind.Left && TryHandleDesktopOrTaskbarDoubleClick(point))
            return Consumed;

        if (button == MouseButtonKind.None || !IsWindowDragEnabled)
            return InputEventHandleResult.None;

        return ToResult(_windowDrag.HandleMouseDown(button, point, args.ExtraInfo));
    }

    public InputEventHandleResult HandleUp(MouseEventArgs args)
    {
        var button = ToButton(args.CurrentKey);
        if (button == MouseButtonKind.None || !IsWindowDragEnabled)
            return InputEventHandleResult.None;

        var point = new ScreenPoint((int)args.Position.X, (int)args.Position.Y);
        return ToResult(_windowDrag.HandleMouseUp(button, point, args.ExtraInfo));
    }

    public InputEventHandleResult HandleMove(MouseEventArgs args)
    {
        if (!IsWindowDragEnabled)
            return InputEventHandleResult.None;

        var point = new ScreenPoint((int)args.Position.X, (int)args.Position.Y);
        return ToResult(_windowDrag.HandleMouseMove(point, args.ExtraInfo));
    }

    public InputEventHandleResult HandleDoubleClick(MouseEventArgs args) => InputEventHandleResult.None;

    public InputEventHandleResult HandleWheel(MouseEventArgs args)
    {
        bool volumeEnabled;
        MouseEnhanceModifier modifier;
        lock (_sync)
        {
            volumeEnabled = _volumeScrollEnabled;
            modifier = _activationModifier;
        }

        if (!volumeEnabled || _windowDrag.IsInteracting)
            return InputEventHandleResult.None;

        var delta = (int)args.Delta;
        return _volumeScroll.TryHandleWheel(modifier, delta) ? Consumed : InputEventHandleResult.None;
    }

    private bool TryHandleDesktopOrTaskbarDoubleClick(ScreenPoint point)
    {
        if (!_doubleClick.TryRegisterLeftDown(point))
            return false;

        bool desktopOn;
        bool taskbarOn;
        lock (_sync)
        {
            desktopOn = _desktopIconsEnabled;
            taskbarOn = _taskbarShowDesktopEnabled;
        }

        // Never SendMessage into Explorer from this WH_MOUSE_LL callback.
        // Desktop: only class-walk here; defer hit-test + toggle. Do not swallow —
        // so icon double-clicks still open, empty area toggle runs after the hook returns.
        if (desktopOn && DesktopShellFeature.IsDesktopSurface(point))
        {
            DesktopShellFeature.ScheduleToggleDesktopIconsIfEmpty(point);
            return false;
        }

        // Taskbar: defer Shell ToggleDesktop; swallow to avoid taskbar side effects.
        if (taskbarOn && DesktopShellFeature.IsTaskbarArea(point))
        {
            DesktopShellFeature.ScheduleToggleShowDesktop();
            return true;
        }

        return false;
    }

    private static InputEventHandleResult ToResult(HookDisposition disposition) => disposition switch
    {
        HookDisposition.Swallow => Consumed,
        HookDisposition.PassWithoutChaining => InputEventHandleResult.Handled,
        _ => InputEventHandleResult.None
    };

    private static MouseButtonKind ToButton(MouseKey key) => key switch
    {
        MouseKey.Left => MouseButtonKind.Left,
        MouseKey.Right => MouseButtonKind.Right,
        _ => MouseButtonKind.None
    };

    private static IReadOnlyList<string> ParseExcludedLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => line.Length > 0)
            .ToArray();
    }

    private static uint MapScanCode(uint vk)
    {
        const uint mapVkToVsc = 0;
        return MapVirtualKeyW(vk, mapVkToVsc);
    }

    private static bool IsExtended(uint vk) =>
        vk is 0xA5 or 0x5C; // RMENU / RWIN commonly extended

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint uCode, uint uMapType);
}
