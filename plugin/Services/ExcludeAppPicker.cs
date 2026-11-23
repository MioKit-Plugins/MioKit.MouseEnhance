using Avalonia.Threading;
using MouseEnhance.Models;
using MouseEnhance.Views;

namespace MouseEnhance.Services;

internal sealed class ExcludeAppPicker
{
    private const int Escape = 0x1B;
    private Window? _owner;
    private nint _ownerHandle;
    private DispatcherTimer? _timer;
    private TaskCompletionSource<string?>? _completion;
    private ExcludeAppPickerOverlay? _overlay;
    private ScreenPoint _origin;
    private int _buttonVirtualKey;
    private bool _hasDragged;
    private nint _highlightedHandle;
    private string? _highlightedProcess;
    private WindowRect _highlightedBounds;

    public Task<string?> PickAsync(Window owner) => Dispatcher.UIThread.CheckAccess()
        ? BeginPick(owner)
        : Dispatcher.UIThread.InvokeAsync(() => BeginPick(owner));

    public void Cancel()
    {
        if (Dispatcher.UIThread.CheckAccess())
            Complete(null);
        else
            Dispatcher.UIThread.Post(() => Complete(null));
    }

    private Task<string?> BeginPick(Window owner)
    {
        var handle = owner.TryGetPlatformHandle()?.Handle ?? 0;
        var button = GrabWindowNative.PrimaryMouseButtonVirtualKey;
        if (_completion is not null || !owner.IsVisible || handle == 0 || !GrabWindowNative.IsKeyDown(button) ||
            !GrabWindowNative.TryGetCursorPosition(out var x, out var y))
        {
            return Task.FromResult<string?>(null);
        }

        _owner = owner;
        _ownerHandle = handle;
        _origin = new ScreenPoint(x, y);
        _buttonVirtualKey = button;
        _hasDragged = false;
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;
        _owner.Closed += OnOwnerClosed;
        _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
        return completion.Task;
    }

    private void OnTick(object? sender, EventArgs args)
    {
        try
        {
            Poll();
        }
        catch (Exception exception)
        {
            Complete(null, exception);
        }
    }

    private void Poll()
    {
        if (_owner is not { IsVisible: true } || !GrabWindowNative.IsWindow(_ownerHandle) ||
            GrabWindowNative.IsKeyDown(Escape) || !GrabWindowNative.TryGetCursorPosition(out var x, out var y))
        {
            Complete(null);
            return;
        }

        var released = !GrabWindowNative.IsKeyDown(_buttonVirtualKey);
        _hasDragged = _hasDragged || Math.Abs(x - _origin.X) >= GrabWindowNative.DragThresholdX ||
                      Math.Abs(y - _origin.Y) >= GrabWindowNative.DragThresholdY;
        if (!_hasDragged)
        {
            if (released)
                Complete(null);
            return;
        }

        // In a remote session the drag feature prefers the foreground application, which
        // is the settings window during an exclusion pick. Here we need the window under the pointer.
        var handle = GrabWindowNative.ResolveTargetWindow(new ScreenPoint(x, y), preferRemoteForeground: false);
        if (handle == 0 || handle == _ownerHandle || handle == _overlay?.TryGetPlatformHandle()?.Handle ||
            !GrabWindowNative.TryGetWindowRect(handle, out var bounds) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            if (released)
                Complete(null);
            else
                HideHighlight();
            return;
        }

        if (released)
        {
            var process = GrabWindowNative.GetProcessFileName(handle);
            Complete(string.IsNullOrWhiteSpace(process) ? null : process);
            return;
        }

        if (_highlightedHandle == handle && _highlightedBounds == bounds)
            return;

        if (_highlightedHandle != handle)
            _highlightedProcess = GrabWindowNative.GetProcessFileName(handle);
        _highlightedHandle = handle;
        _highlightedBounds = bounds;
        if (string.IsNullOrWhiteSpace(_highlightedProcess))
        {
            _overlay?.Hide();
            return;
        }

        if (_overlay is null)
        {
            _overlay = new ExcludeAppPickerOverlay();
            _overlay.Closed += OnOverlayClosed;
            WindowManager.SetOwnerId(_overlay, MouseEnhanceConst.PluginId);
        }

        _overlay.MoveTo(bounds.Left, bounds.Top, bounds.Width, bounds.Height, null, _highlightedProcess);
    }

    private void HideHighlight()
    {
        _highlightedHandle = 0;
        _highlightedProcess = null;
        _overlay?.Hide();
    }

    private void OnOwnerClosed(object? sender, EventArgs args) => Complete(null);

    private void OnOverlayClosed(object? sender, EventArgs args)
    {
        if (sender is ExcludeAppPickerOverlay overlay)
            overlay.Closed -= OnOverlayClosed;
        _overlay = null;
        Complete(null);
    }

    private void Complete(string? process, Exception? error = null)
    {
        var completion = _completion;
        if (completion is null)
            return;

        _completion = null;
        _timer!.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
        _owner!.Closed -= OnOwnerClosed;
        _owner = null;
        _ownerHandle = 0;
        _highlightedHandle = 0;
        _highlightedProcess = null;
        var overlay = _overlay;
        _overlay = null;
        try
        {
            if (overlay is not null)
            {
                overlay.Closed -= OnOverlayClosed;
                overlay.Close();
            }
        }
        catch (Exception closeError)
        {
            error ??= closeError;
        }

        if (error is null)
            completion.SetResult(process);
        else
            completion.SetException(error);
    }
}
