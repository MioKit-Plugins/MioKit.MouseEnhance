using MouseEnhance.Models;

namespace MouseEnhance;

public static partial class MouseEnhanceExtension
{
    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<bool?> WindowDragEnabledProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(MouseEnhanceConst.WindowDragEnabledPropertyGuid)
            .WithName("WindowDragEnabled")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<MouseEnhanceModifier?> ActivationModifierProperty { get; } =
        EavPropertyBuilder<MouseEnhanceModifier?>.Create()
            .WithId(MouseEnhanceConst.ActivationModifierPropertyGuid)
            .WithName("ActivationModifier")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Integer)
            .WithPropertyConvert(EnumIntegerConvert<MouseEnhanceModifier>.Instance)
            .WithDefaultValue(MouseEnhanceModifier.Alt)
            .Build();

    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<bool?> PreventAltWindowMenuProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(MouseEnhanceConst.PreventAltWindowMenuPropertyGuid)
            .WithName("PreventAltWindowMenu")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<bool?> RightClickResizeEnabledProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(MouseEnhanceConst.RightClickResizeEnabledPropertyGuid)
            .WithName("RightClickResizeEnabled")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<string?> ExcludedAppsProperty { get; } =
        EavPropertyBuilder<string?>.Create()
            .WithId(MouseEnhanceConst.ExcludedAppsPropertyGuid)
            .WithName("ExcludedApps")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.String)
            .WithDefaultValue(string.Empty)
            .Build();

    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<string?> ModifierExcludedAppsProperty { get; } =
        EavPropertyBuilder<string?>.Create()
            .WithId(MouseEnhanceConst.ModifierExcludedAppsPropertyGuid)
            .WithName("ModifierExcludedApps")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.String)
            .WithDefaultValue(string.Empty)
            .Build();

    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<bool?> VolumeScrollEnabledProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(MouseEnhanceConst.VolumeScrollEnabledPropertyGuid)
            .WithName("VolumeScrollEnabled")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<bool?> DesktopIconsToggleEnabledProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(MouseEnhanceConst.DesktopIconsToggleEnabledPropertyGuid)
            .WithName("DesktopIconsToggleEnabled")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(false)
            .Build();

    [EavRelation(typeof(IMouseEnhancePluginFeature))]
    public static EavProperty<bool?> TaskbarShowDesktopEnabledProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(MouseEnhanceConst.TaskbarShowDesktopEnabledPropertyGuid)
            .WithName("TaskbarShowDesktopEnabled")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(false)
            .Build();
}
