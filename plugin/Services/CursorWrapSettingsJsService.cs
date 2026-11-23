using System.Text.Json;
using Avalonia.Threading;
using MioKit.Webview2.Core;
using MouseEnhance.Models;

namespace MouseEnhance.Services;

[JsService("CursorWrapSettings")]
internal sealed class CursorWrapSettingsJsService : IDisposable
{
    private readonly MouseEnhancePlugin _plugin;
    private readonly CursorWrapController _controller;
    private bool _disposed;

    public CursorWrapSettingsJsService(MouseEnhancePlugin plugin, CursorWrapController controller)
    {
        _plugin = plugin;
        _controller = controller;
        _controller.StateChanged += OnStateChanged;
    }

    public event EventHandler<CursorWrapSettingsSnapshot>? StateChanged;

    public CursorWrapSettingsSnapshot Load()
    {
        var (settings, active) = _controller.ReadState();
        return new(settings.Enabled, settings.AutoActivate, settings.DisableWhileDragging,
            settings.Mode, settings.Activation, settings.DisableForSingleMonitor, active);
    }

    public CursorWrapSettingsSnapshot Save(string field, string valueJson)
    {
        if (valueJson is null || valueJson.Length > 24)
            throw InvalidValue();

        JsonDocument document;
        try { document = JsonDocument.Parse(valueJson); }
        catch (JsonException) { throw InvalidValue(); }

        using (document)
            return Apply(settings => UpdateField(settings, field, document.RootElement));
    }

    private static CursorWrapSettings UpdateField(CursorWrapSettings settings, string field, JsonElement value)
    {
        try
        {
            return field switch
            {
                "enabled" => settings with { Enabled = value.GetBoolean() },
                "autoActivate" => settings with { AutoActivate = value.GetBoolean() },
                "disableWhileDragging" => settings with { DisableWhileDragging = value.GetBoolean() },
                "disableForSingleMonitor" => settings with { DisableForSingleMonitor = value.GetBoolean() },
                "mode" => settings with { Mode = ReadMode(value) },
                "activation" => settings with { Activation = ReadActivation(value) },
                _ => throw new ArgumentException("未知的光标环绕设置。")
            };
        }
        catch (InvalidOperationException)
        {
            throw InvalidValue();
        }
    }

    public CursorWrapSettingsSnapshot ToggleActive()
    {
        _controller.ToggleActive();
        return Load();
    }

    private CursorWrapSettingsSnapshot Apply(Func<CursorWrapSettings, CursorWrapSettings> update)
    {
        _controller.UpdateSettings(update, settings => _plugin.SetCursorWrapSettings(settings));
        return Load();
    }

    private void OnStateChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed)
            StateChanged?.Invoke(this, Load());
    });

    void IDisposable.Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _controller.StateChanged -= OnStateChanged;
    }

    private static CursorWrapMode ReadMode(JsonElement value) => value.GetString() switch
    {
        "both" => CursorWrapMode.Both,
        "vertical" => CursorWrapMode.Vertical,
        "horizontal" => CursorWrapMode.Horizontal,
        _ => throw InvalidValue()
    };

    private static CursorWrapActivation ReadActivation(JsonElement value) => value.GetString() switch
    {
        "always" => CursorWrapActivation.Always,
        "control" => CursorWrapActivation.Control,
        "shift" => CursorWrapActivation.Shift,
        _ => throw InvalidValue()
    };

    private static ArgumentException InvalidValue() => new("光标环绕设置值超出允许范围或格式有误。");
}

internal sealed record CursorWrapSettingsSnapshot(bool Enabled, bool AutoActivate, bool DisableWhileDragging,
    CursorWrapMode Mode, CursorWrapActivation Activation, bool DisableForSingleMonitor, bool IsActive);
