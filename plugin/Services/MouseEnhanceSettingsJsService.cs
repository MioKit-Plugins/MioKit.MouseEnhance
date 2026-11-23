using System.Text.Json;
using MioKit.Webview2.Core;
using MouseEnhance.Models;

namespace MouseEnhance.Services;

[JsService("MouseEnhanceSettings")]
internal sealed class MouseEnhanceSettingsJsService(
    MouseEnhancePlugin plugin,
    MouseEnhanceService service,
    ExcludeAppPicker picker,
    Window owner)
{
    public MouseEnhanceSettingsSnapshot Load() => service.WithSettingsLock(ReadSnapshot);

    private MouseEnhanceSettingsSnapshot ReadSnapshot() => new(
        service.IsWindowDragEnabled,
        plugin.GetVolumeScrollEnabled() != false,
        plugin.GetDesktopIconsToggleEnabled() == true,
        plugin.GetTaskbarShowDesktopEnabled() == true,
        plugin.GetActivationModifier() == MouseEnhanceModifier.Win ? "win" : "alt",
        plugin.GetRightClickResizeEnabled() != false,
        plugin.GetPreventAltWindowMenu() != false,
        plugin.GetExcludedApps() ?? string.Empty,
        plugin.GetModifierExcludedApps() ?? string.Empty);

    public MouseEnhanceSettingsSnapshot Save(string field, string valueJson)
    {
        using var document = ParseValue(valueJson);
        return service.WithSettingsLock(() => SaveValue(field, document.RootElement));
    }

    private MouseEnhanceSettingsSnapshot SaveValue(string field, JsonElement value)
    {
        switch (field)
        {
            case "windowDragEnabled":
                if (ReadBoolean(value))
                    service.EnableWindowDrag();
                else
                    service.DisableWindowDrag();
                return ReadSnapshot();
            case "volumeScrollEnabled":
                plugin.SetVolumeScrollEnabled(ReadBoolean(value));
                break;
            case "desktopIconsToggleEnabled":
                plugin.SetDesktopIconsToggleEnabled(ReadBoolean(value));
                break;
            case "taskbarShowDesktopEnabled":
                plugin.SetTaskbarShowDesktopEnabled(ReadBoolean(value));
                break;
            case "activationModifier":
                plugin.SetActivationModifier(ReadModifier(value));
                break;
            case "rightClickResizeEnabled":
                plugin.SetRightClickResizeEnabled(ReadBoolean(value));
                break;
            case "preventAltWindowMenu":
                plugin.SetPreventAltWindowMenu(ReadBoolean(value));
                break;
            default:
                throw new ArgumentException("未知的鼠标增强设置。", nameof(field));
        }

        service.ReloadSettingsFromPlugin();
        return ReadSnapshot();
    }

    public MouseEnhanceSettingsSnapshot ApplyExclusions(string? excludedApps, string? modifierExcludedApps)
    {
        var excluded = NormalizeLines(excludedApps);
        var modifierExcluded = NormalizeLines(modifierExcludedApps);

        return service.WithSettingsLock(() =>
        {
            plugin.SetExcludedApps(excluded);
            plugin.SetModifierExcludedApps(modifierExcluded);
            service.ReloadSettingsFromPlugin();
            return ReadSnapshot();
        });
    }

    public Task<string?> PickApplication() => picker.PickAsync(owner);

    public void CancelPick() => picker.Cancel();

    private static JsonDocument ParseValue(string? valueJson)
    {
        if (valueJson is null || valueJson.Length > 32)
            throw InvalidValue();

        try
        {
            return JsonDocument.Parse(valueJson);
        }
        catch (JsonException)
        {
            throw InvalidValue();
        }
    }

    private static bool ReadBoolean(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw InvalidValue()
    };

    private static MouseEnhanceModifier ReadModifier(JsonElement value) =>
        (value.ValueKind == JsonValueKind.String ? value.GetString() : null) switch
        {
            "alt" => MouseEnhanceModifier.Alt,
            "win" => MouseEnhanceModifier.Win,
            _ => throw InvalidValue()
        };

    private static string NormalizeLines(string? text) => string.Join(
        Environment.NewLine,
        (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static ArgumentException InvalidValue() => new("鼠标增强设置值的格式有误。");
}

internal sealed record MouseEnhanceSettingsSnapshot(
    bool WindowDragEnabled,
    bool VolumeScrollEnabled,
    bool DesktopIconsToggleEnabled,
    bool TaskbarShowDesktopEnabled,
    string ActivationModifier,
    bool RightClickResizeEnabled,
    bool PreventAltWindowMenu,
    string ExcludedApps,
    string ModifierExcludedApps);
