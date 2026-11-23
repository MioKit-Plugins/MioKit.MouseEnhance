using System.Text.Json;
using MioKit.Webview2.Core;
using MouseEnhance.Models;

namespace MouseEnhance.Services;

[JsService("FindMouseSettings")]
internal sealed class FindMouseSettingsJsService(MouseEnhancePlugin plugin, FindMouseController controller)
{
    public FindMouseSettings Load() => plugin.ReadFindMouseSettings();

    public FindMouseSettings Save(string field, string valueJson)
    {
        if (valueJson is null || valueJson.Length > 32)
            throw InvalidValue();

        var settings = Load();
        FindMouseSettings updated;
        try
        {
            using var document = JsonDocument.Parse(valueJson);
            var value = document.RootElement;
            updated = field switch
            {
                "enabled" => settings with { Enabled = value.GetBoolean() },
                "doubleCtrlEnabled" => settings with { DoubleCtrlEnabled = value.GetBoolean() },
                "shakeEnabled" => settings with { ShakeEnabled = value.GetBoolean() },
                "spotlightColor" => settings with { SpotlightColor = value.GetString() ?? string.Empty },
                "spotlightDiameter" => settings with { SpotlightDiameter = value.GetInt32() },
                "maskOpacity" => settings with { MaskOpacity = value.GetInt32() },
                "glowOpacity" => settings with { GlowOpacity = value.GetInt32() },
                "animationDurationMs" => settings with { AnimationDurationMs = value.GetInt32() },
                "holdDurationMs" => settings with { HoldDurationMs = value.GetInt32() },
                "shakeThreshold" => settings with { ShakeThreshold = value.GetInt32() },
                _ => throw new ArgumentException("未知的寻找鼠标设置。")
            };
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        {
            throw InvalidValue();
        }

        if (updated != updated.Normalize())
            throw InvalidValue();

        plugin.SetFindMouseSettings(updated);
        controller.UpdateSettings(updated);
        return updated;
    }

    public Task<FindMouseSettings> Preview() => controller.PreviewAsync();

    private static ArgumentException InvalidValue() => new("寻找鼠标设置值超出允许范围或格式有误。");
}
