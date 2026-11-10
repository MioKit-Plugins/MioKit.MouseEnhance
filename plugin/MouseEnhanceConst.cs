namespace MouseEnhance;

public static class MouseEnhanceConst
{
    public const string PluginId = "com.tinyisle.plugin.mouse-enhance";

    public const string PluginTypeId = "143FC4A2-3E80-41D7-B61A-F211CBFA0CF1";
    public static readonly Guid PluginType = Guid.Parse(PluginTypeId);

    public const string SearchGroupTypeId = "48E5D94A-4968-46A2-9D84-0DA83A40E33D";
    public static readonly Guid SearchGroupType = Guid.Parse(SearchGroupTypeId);

    public const string StartNodeTypeId = "A79D7657-9086-493C-8F27-8DFF711FB66F";
    public static readonly Guid StartNodeType = Guid.Parse(StartNodeTypeId);

    public const string StopNodeTypeId = "5978C5F2-6D86-49AD-9657-9624691E54FD";
    public static readonly Guid StopNodeType = Guid.Parse(StopNodeTypeId);

    public const string SearchGroupId = PluginId + ".search-group";
    public const string StartNodeId = PluginId + ".start";
    public const string StopNodeId = PluginId + ".stop";

    public const string WindowDragEnabledPropertyId = "01229891-004D-4594-96A4-F9E9D02F6D2A";
    public static readonly Guid WindowDragEnabledPropertyGuid = Guid.Parse(WindowDragEnabledPropertyId);

    public const string ActivationModifierPropertyId = "6AFEF32F-FD71-4CF8-A4C7-87348D3FC9AA";
    public static readonly Guid ActivationModifierPropertyGuid = Guid.Parse(ActivationModifierPropertyId);

    public const string PreventAltWindowMenuPropertyId = "E040D3AF-A887-427B-9205-F632DFA8FAE6";
    public static readonly Guid PreventAltWindowMenuPropertyGuid = Guid.Parse(PreventAltWindowMenuPropertyId);

    public const string RightClickResizeEnabledPropertyId = "4051BA7B-DA01-4B81-9E7D-0D68AB63F3DD";
    public static readonly Guid RightClickResizeEnabledPropertyGuid = Guid.Parse(RightClickResizeEnabledPropertyId);

    public const string ExcludedAppsPropertyId = "566B7A56-FEC7-4187-97FF-99145B29FA4F";
    public static readonly Guid ExcludedAppsPropertyGuid = Guid.Parse(ExcludedAppsPropertyId);

    public const string ModifierExcludedAppsPropertyId = "CB17EDF6-B0F7-4978-98CB-CBA76F396231";
    public static readonly Guid ModifierExcludedAppsPropertyGuid = Guid.Parse(ModifierExcludedAppsPropertyId);

    public const string VolumeScrollEnabledPropertyId = "A2B92852-A662-4FED-8344-A5B6AD34E116";
    public static readonly Guid VolumeScrollEnabledPropertyGuid = Guid.Parse(VolumeScrollEnabledPropertyId);

    public const string DesktopIconsToggleEnabledPropertyId = "31050EF4-D7B5-4041-9063-DF6AD74FECA0";
    public static readonly Guid DesktopIconsToggleEnabledPropertyGuid =
        Guid.Parse(DesktopIconsToggleEnabledPropertyId);

    public const string TaskbarShowDesktopEnabledPropertyId = "F7B0BDA1-697A-4573-BB5C-B81D82A1F5EA";
    public static readonly Guid TaskbarShowDesktopEnabledPropertyGuid =
        Guid.Parse(TaskbarShowDesktopEnabledPropertyId);

    public const string InputHandlerId = PluginId + ".hooks";

    /// <summary>Before host global hotkey handler (order 100).</summary>
    public const ushort InputHandlerOrder = 40;
}
