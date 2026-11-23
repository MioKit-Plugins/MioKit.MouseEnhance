using MouseEnhance.Models;
using MouseEnhance.Services.Features;

namespace MouseEnhance.Services;

internal readonly record struct CursorWrapInput(
    bool ControlHeld = false,
    bool ShiftHeld = false,
    bool ButtonHeld = false,
    bool WindowDragActive = false,
    bool IsSynthetic = false);

internal sealed record CursorWrapMove(ScreenPoint Previous, ScreenPoint Current, ScreenPoint Target);

internal sealed class CursorWrapController
{
    private readonly object _configurationSync = new();
    private readonly object _sync = new();
    private CursorWrapSettings _settings = new();
    private bool _running;
    private bool _active;
    private CursorWrapMonitor[] _monitors = [];
    private ScreenPoint? _previous;
    private CursorWrapMove? _pendingMove;

    public event EventHandler? StateChanged;

    public CursorWrapSettings Settings
    {
        get { lock (_sync) return _settings; }
    }

    public bool IsActive
    {
        get { lock (_sync) return _active; }
    }

    public (CursorWrapSettings Settings, bool IsActive) ReadState()
    {
        lock (_sync)
            return (_settings, _active);
    }

    public void Start(CursorWrapSettings settings)
    {
        lock (_configurationSync)
        {
            lock (_sync)
            {
                if (_running)
                    return;
                _running = true;
                _settings = settings;
                _active = settings.Enabled && settings.AutoActivate;
            }
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_configurationSync)
        {
            lock (_sync)
            {
                _running = false;
                _active = false;
                _monitors = [];
                ResetMovement();
            }
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateSettings(Func<CursorWrapSettings, CursorWrapSettings> update,
        Action<CursorWrapSettings> persist)
    {
        lock (_configurationSync)
        {
            ApplySettingsCore(update(_settings), persist);
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplySettingsCore(CursorWrapSettings settings, Action<CursorWrapSettings> persist)
    {
        persist(settings);
        lock (_sync)
        {
            _active = _running && settings.Enabled && (!_settings.Enabled || _active);
            _settings = settings;
            ResetMovement();
        }
    }

    public void SetFeatureActive(bool active, Action<CursorWrapSettings> persist)
    {
        lock (_configurationSync)
        {
            if (!_running)
                return;

            if (active && !_settings.Enabled)
            {
                ApplySettingsCore(_settings with { Enabled = true }, persist);
            }
            else if (!TrySetActive(active))
            {
                return;
            }
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TrySetActive(bool active)
    {
        lock (_sync)
        {
            if (!_running || !_settings.Enabled || _active == active)
                return false;
            _active = active;
            ResetMovement();
            return true;
        }
    }

    public void ToggleActive()
    {
        lock (_sync)
        {
            if (!_running || !_settings.Enabled)
                return;
            _active = !_active;
            ResetMovement();
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateMonitors(CursorWrapMonitor[] monitors)
    {
        lock (_sync)
        {
            if (!_running || _monitors.AsSpan().SequenceEqual(monitors))
                return;
            _monitors = monitors;
            ResetMovement();
        }
    }

    public CursorWrapMove? ObserveMove(ScreenPoint current, CursorWrapInput input)
    {
        lock (_sync)
        {
            if (!_active || input.IsSynthetic)
            {
                ResetMovement();
                return null;
            }

            var previous = _previous;
            // Keep a point on the source display when the hook reports motion into a gap.
            _previous = CursorWrapGeometry.ClampToDesktop(_monitors, previous, current);
            if (!CanWrap(input))
            {
                _pendingMove = null;
                return null;
            }

            if (_pendingMove?.Current == current)
                return null;

            _pendingMove = previous is { } from &&
                CursorWrapGeometry.TryGetTarget(_monitors, from, current, _settings.Mode, out var target)
                ? new CursorWrapMove(from, current, target) : null;
            return _pendingMove;
        }
    }

    public bool TryExecuteMove(CursorWrapMove move, CursorWrapInput input,
        Func<ScreenPoint, ScreenPoint, ScreenPoint, IReadOnlyList<CursorWrapMonitor>, bool> tryMove)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(move, _pendingMove))
                return false;

            _pendingMove = null;
            if (!CanWrap(input) || !tryMove(move.Previous, move.Current, move.Target, _monitors))
                return false;

            _previous = move.Target;
            return true;
        }
    }

    private bool CanWrap(CursorWrapInput input) => _active && !input.IsSynthetic && !input.WindowDragActive &&
        (!_settings.DisableWhileDragging || !input.ButtonHeld) &&
        (!_settings.DisableForSingleMonitor || _monitors.Length > 1) &&
        (_settings.Activation switch
        {
            CursorWrapActivation.Control => input.ControlHeld,
            CursorWrapActivation.Shift => input.ShiftHeld,
            _ => true
        });

    private void ResetMovement()
    {
        _previous = null;
        _pendingMove = null;
    }
}
