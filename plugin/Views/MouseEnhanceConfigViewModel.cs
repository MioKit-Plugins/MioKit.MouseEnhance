using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MouseEnhance.Models;
using MouseEnhance.Services;
using Ti.Avalonia.Shadcn.Controls;
using Ti.Avalonia.Shadcn.Controls.Toast;
using Ti.Avalonia.Shadcn.Extensions;

namespace MouseEnhance.Views;

internal partial class MouseEnhanceConfigViewModel : ObservableObject
{
    private readonly MouseEnhancePlugin _plugin;
    private readonly MouseEnhanceService _service;
    private bool _isLoaded;
    private string _appliedExcludedApps = string.Empty;
    private string _appliedModifierExcludedApps = string.Empty;

    public MouseEnhanceConfigViewModel()
    {
        _plugin = null!;
        _service = null!;
        _isLoaded = false;
    }

    public MouseEnhanceConfigViewModel(MouseEnhancePlugin plugin, MouseEnhanceService service)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(service);
        _plugin = plugin;
        _service = service;
    }

    /// <summary>Preview / design shell without plugin DI.</summary>
    public static MouseEnhanceConfigViewModel CreateDesignPreview() => new();

    public ShadWindow? Window { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeCardTitle))]
    [NotifyPropertyChangedFor(nameof(VolumeCardDescription))]
    [NotifyPropertyChangedFor(nameof(WindowDragCardTitle))]
    [NotifyPropertyChangedFor(nameof(WindowDragCardDescription))]
    [NotifyPropertyChangedFor(nameof(WindowDragSectionOpacity))]
    private bool _volumeScrollEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowDragSectionOpacity))]
    [NotifyPropertyChangedFor(nameof(GestureCardsOpacity))]
    [NotifyPropertyChangedFor(nameof(ResizeHintOpacity))]
    private bool _windowDragEnabled = true;

    [ObservableProperty]
    private bool _desktopIconsToggleEnabled;

    [ObservableProperty]
    private bool _taskbarShowDesktopEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAltSelected))]
    [NotifyPropertyChangedFor(nameof(IsWinSelected))]
    [NotifyPropertyChangedFor(nameof(AltMenuSettingOpacity))]
    [NotifyPropertyChangedFor(nameof(ModifierDisplayName))]
    [NotifyPropertyChangedFor(nameof(MoveGestureHint))]
    [NotifyPropertyChangedFor(nameof(ResizeGestureHint))]
    [NotifyPropertyChangedFor(nameof(VolumeCardTitle))]
    [NotifyPropertyChangedFor(nameof(VolumeCardDescription))]
    [NotifyPropertyChangedFor(nameof(WindowDragCardTitle))]
    [NotifyPropertyChangedFor(nameof(WindowDragCardDescription))]
    private MouseEnhanceModifier _activationModifier = MouseEnhanceModifier.Alt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResizeGestureHint))]
    [NotifyPropertyChangedFor(nameof(ResizeHintOpacity))]
    private bool _rightClickResizeEnabled = true;

    [ObservableProperty]
    private bool _preventAltWindowMenu = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnappliedExcludeChanges))]
    [NotifyPropertyChangedFor(nameof(UnappliedChangesOpacity))]
    [NotifyCanExecuteChangedFor(nameof(ApplyExclusionsCommand))]
    private string _excludedAppsDraft = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnappliedExcludeChanges))]
    [NotifyPropertyChangedFor(nameof(UnappliedChangesOpacity))]
    [NotifyCanExecuteChangedFor(nameof(ApplyExclusionsCommand))]
    private string _modifierExcludedAppsDraft = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowPickHintOpacity))]
    private bool _isPickingWindowExclude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierPickHintOpacity))]
    private bool _isPickingModifierExclude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowPickHintOpacity))]
    private string _windowPickHint = "拖到窗口后松开";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierPickHintOpacity))]
    private string _modifierPickHint = "拖到窗口后松开";

    public double WindowDragSectionOpacity => WindowDragEnabled ? 1 : 0.45;
    public double GestureCardsOpacity => WindowDragEnabled ? 1 : 0.45;
    public bool IsAltSelected => ActivationModifier == MouseEnhanceModifier.Alt;
    public bool IsWinSelected => ActivationModifier == MouseEnhanceModifier.Win;
    public double AltMenuSettingOpacity => IsAltSelected ? 1 : 0.45;
    public string ModifierDisplayName => ActivationModifier == MouseEnhanceModifier.Win ? "Win" : "Alt";
    public string VolumeCardTitle => $"{ModifierDisplayName}+滚轮调节音量";
    public string VolumeCardDescription => $"按住{ModifierDisplayName}键滚动鼠标滚轮调节系统音量。";
    public string WindowDragCardTitle => $"{ModifierDisplayName}+拖动窗口";
    public string WindowDragCardDescription =>
        $"按住{ModifierDisplayName}：左键拖动移动窗口，右键拖动调整大小。";
    public string MoveGestureHint => $"{ModifierDisplayName} + 左键拖动";
    public string ResizeGestureHint => RightClickResizeEnabled
        ? $"{ModifierDisplayName} + 右键拖动"
        : "已关闭";
    public double ResizeHintOpacity =>
        !RightClickResizeEnabled || !WindowDragEnabled ? 0.45 : 1;

    public bool HasUnappliedExcludeChanges =>
        !string.Equals(NormalizeLines(ExcludedAppsDraft), NormalizeLines(_appliedExcludedApps), StringComparison.Ordinal) ||
        !string.Equals(
            NormalizeLines(ModifierExcludedAppsDraft),
            NormalizeLines(_appliedModifierExcludedApps),
            StringComparison.Ordinal);

    public double UnappliedChangesOpacity => HasUnappliedExcludeChanges ? 1 : 0;
    public double WindowPickHintOpacity => IsPickingWindowExclude ? 1 : 0;
    public double ModifierPickHintOpacity => IsPickingModifierExclude ? 1 : 0;

    public void LoadFromPlugin()
    {
        _isLoaded = false;

        WindowDragEnabled = _service.IsWindowDragEnabled;
        VolumeScrollEnabled = _plugin.GetVolumeScrollEnabled() != false;
        DesktopIconsToggleEnabled = _plugin.GetDesktopIconsToggleEnabled() == true;
        TaskbarShowDesktopEnabled = _plugin.GetTaskbarShowDesktopEnabled() == true;
        ActivationModifier = _plugin.GetActivationModifier() ?? MouseEnhanceModifier.Alt;
        RightClickResizeEnabled = _plugin.GetRightClickResizeEnabled() != false;
        PreventAltWindowMenu = _plugin.GetPreventAltWindowMenu() != false;

        _appliedExcludedApps = _plugin.GetExcludedApps() ?? string.Empty;
        _appliedModifierExcludedApps = _plugin.GetModifierExcludedApps() ?? string.Empty;
        ExcludedAppsDraft = _appliedExcludedApps;
        ModifierExcludedAppsDraft = _appliedModifierExcludedApps;

        _isLoaded = true;
        OnPropertyChanged(nameof(HasUnappliedExcludeChanges));
        OnPropertyChanged(nameof(UnappliedChangesOpacity));
        ApplyExclusionsCommand.NotifyCanExecuteChanged();
    }

    public void AppendWindowExcludeProcess(string? processFileName)
    {
        ExcludedAppsDraft = AppendLine(ExcludedAppsDraft, processFileName);
    }

    public void AppendModifierExcludeProcess(string? processFileName)
    {
        ModifierExcludedAppsDraft = AppendLine(ModifierExcludedAppsDraft, processFileName);
    }

    public void SetWindowPickPreview(string? processFileName)
    {
        WindowPickHint = string.IsNullOrWhiteSpace(processFileName)
            ? "拖到窗口后松开"
            : $"松开以添加 {processFileName}";
    }

    public void SetModifierPickPreview(string? processFileName)
    {
        ModifierPickHint = string.IsNullOrWhiteSpace(processFileName)
            ? "拖到窗口后松开"
            : $"松开以添加 {processFileName}";
    }

    partial void OnWindowDragEnabledChanged(bool value)
    {
        if (!_isLoaded)
            return;

        if (value)
            _service.EnableWindowDrag();
        else
            _service.DisableWindowDrag();
    }

    partial void OnVolumeScrollEnabledChanged(bool value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetVolumeScrollEnabled(value);
        _service.ReloadSettingsFromPlugin();
    }

    partial void OnDesktopIconsToggleEnabledChanged(bool value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetDesktopIconsToggleEnabled(value);
        _service.ReloadSettingsFromPlugin();
    }

    partial void OnTaskbarShowDesktopEnabledChanged(bool value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetTaskbarShowDesktopEnabled(value);
        _service.ReloadSettingsFromPlugin();
    }

    partial void OnActivationModifierChanged(MouseEnhanceModifier value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetActivationModifier(value);
        _service.ReloadSettingsFromPlugin();
    }

    partial void OnRightClickResizeEnabledChanged(bool value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetRightClickResizeEnabled(value);
        _service.ReloadSettingsFromPlugin();
    }

    partial void OnPreventAltWindowMenuChanged(bool value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetPreventAltWindowMenu(value);
        _service.ReloadSettingsFromPlugin();
    }

    [RelayCommand]
    private void SelectAltModifier()
    {
        ActivationModifier = MouseEnhanceModifier.Alt;
    }

    [RelayCommand]
    private void SelectWinModifier()
    {
        ActivationModifier = MouseEnhanceModifier.Win;
    }

    [RelayCommand(CanExecute = nameof(HasUnappliedExcludeChanges))]
    private void ApplyExclusions()
    {
        var excluded = NormalizeLines(ExcludedAppsDraft);
        var modifierExcluded = NormalizeLines(ModifierExcludedAppsDraft);

        ExcludedAppsDraft = excluded;
        ModifierExcludedAppsDraft = modifierExcluded;
        _appliedExcludedApps = excluded;
        _appliedModifierExcludedApps = modifierExcluded;

        _plugin.SetExcludedApps(excluded);
        _plugin.SetModifierExcludedApps(modifierExcluded);
        _service.ReloadSettingsFromPlugin();

        OnPropertyChanged(nameof(HasUnappliedExcludeChanges));
        OnPropertyChanged(nameof(UnappliedChangesOpacity));
        ApplyExclusionsCommand.NotifyCanExecuteChanged();

        Window?.GetShadToastManager()
            .CreateToast()
            .WithBasicSuccess("已更新排除列表")
            .Queue();
    }

    private static string AppendLine(string current, string? chip)
    {
        if (string.IsNullOrWhiteSpace(chip))
            return current;

        var lines = (current ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (lines.Any(line => string.Equals(line, chip, StringComparison.OrdinalIgnoreCase)))
            return string.Join(Environment.NewLine, lines);

        lines.Add(chip.Trim());
        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
