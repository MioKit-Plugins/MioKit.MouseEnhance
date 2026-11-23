using MouseEnhance.Models;
using MouseEnhance.Nodes;
using MouseEnhance.Services;

namespace MouseEnhance;

[EavType(MouseEnhanceConst.PluginTypeId)]
public class MouseEnhancePlugin : PluginBase,
    IMouseEnhancePluginFeature,
    IPluginMainWindowFeature,
    IInvokeFeature,
    ISearchableFeature,
    IUseFeature,
    IHotkeyFeature,
    IAliasNameFeature,
    IResultActionProviderFeature
{
    public MouseEnhancePlugin() : base(MouseEnhanceConst.PluginId)
    {
        Name = "鼠标增强";
        Description = "滚轮调音量、窗口拖动、桌面快捷手势、寻找鼠标和光标环绕";
        PreloadPropertySource =
        [
            ..PreloadPropertySource,
            MouseEnhanceExtension.WindowDragEnabledProperty,
            MouseEnhanceExtension.ActivationModifierProperty,
            MouseEnhanceExtension.PreventAltWindowMenuProperty,
            MouseEnhanceExtension.RightClickResizeEnabledProperty,
            MouseEnhanceExtension.ExcludedAppsProperty,
            MouseEnhanceExtension.ModifierExcludedAppsProperty,
            MouseEnhanceExtension.VolumeScrollEnabledProperty,
            MouseEnhanceExtension.DesktopIconsToggleEnabledProperty,
            MouseEnhanceExtension.TaskbarShowDesktopEnabledProperty,
            MouseEnhanceExtension.FindMouseSettingsProperty,
            MouseEnhanceExtension.CursorWrapSettingsProperty,
            AliasNameExtension.AliasNameProperty,
            HotkeyExtension.HotkeyProperty,
            RecentExtension.UseCountProperty,
            RecentExtension.LastUseTimeProperty
        ];
    }

    public override Guid MioType => MouseEnhanceConst.PluginType;

    internal FindMouseSettings ReadFindMouseSettings() =>
        (this.GetFindMouseSettings() ?? new FindMouseSettings()).Normalize();

    internal CursorWrapSettings ReadCursorWrapSettings() =>
        (this.GetCursorWrapSettings() ?? new CursorWrapSettings()).Normalize();

    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataProvider = Context.GetRequiredService<IMioDataProvider>();
        var groupExisted = await dataProvider.ExistsAsync(MouseEnhanceConst.SearchGroupId);
        var startExisted = await dataProvider.ExistsAsync(MouseEnhanceConst.StartNodeId);
        var stopExisted = await dataProvider.ExistsAsync(MouseEnhanceConst.StopNodeId);

        await EnsureSubTreeLoadedAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var defaults = new Dictionary<MioObject, Dictionary<IAttachEavProperty, object>>();
        if (!groupExisted)
        {
            var group = this.FindFirstDescendant<MouseEnhanceSearchGroup>(
                node => node.Id == MouseEnhanceConst.SearchGroupId)!;
            group.Name = "鼠标增强";
            group.Description = "打开配置，或控制窗口拖动和光标环绕";
            defaults[group] = CreateNameDefaults(group);
        }

        if (!startExisted)
        {
            var start = this.FindFirstDescendant<StartMouseEnhanceNode>(
                node => node.Id == MouseEnhanceConst.StartNodeId)!;
            start.Name = "启用窗口拖动";
            start.Description = "仅启用窗口拖动手势，不打开配置窗口";
            defaults[start] = CreateNameDefaults(start);
        }

        if (!stopExisted)
        {
            var stop = this.FindFirstDescendant<StopMouseEnhanceNode>(
                node => node.Id == MouseEnhanceConst.StopNodeId)!;
            stop.Name = "停用窗口拖动";
            stop.Description = "仅停用窗口拖动手势，不打开配置窗口";
            defaults[stop] = CreateNameDefaults(stop);
        }

        var startCursorWrap = this.FindFirstDescendant<StartCursorWrapNode>(
            node => node.Id == MouseEnhanceConst.StartCursorWrapNodeId)!;
        var stopCursorWrap = this.FindFirstDescendant<StopCursorWrapNode>(
            node => node.Id == MouseEnhanceConst.StopCursorWrapNodeId)!;
        InitializeCursorWrapNode(startCursorWrap, "启动光标环绕", "启用并激活光标环绕");
        InitializeCursorWrapNode(stopCursorWrap, "停止光标环绕", "暂停本次运行的光标环绕");

        if (defaults.Count > 0)
            await dataProvider.SaveObjectsAsync(defaults.Keys.ToArray(), defaults);

        void InitializeCursorWrapNode(CursorWrapNode node, string name, string description)
        {
            if (!string.IsNullOrWhiteSpace(node.Name))
                return;

            node.Name = name;
            node.Description = description;
            defaults[node] = CreateNameDefaults(node);
        }
    }

    public override Task EnsureSubTreeLoadedAsync()
        => EnsureTreeLoadedAsync<MouseEnhanceSearchGroup>(MouseEnhanceConst.SearchGroupId);

    public void OpenPluginMainWindow()
    {
        MouseEnhanceRegister.Instance.ComponentContext
            .Resolve<MouseEnhanceWindowService>()
            .ShowConfigWindowAsync();
    }

    public async Task InvokeAsync(InvokeContext context)
    {
        OpenPluginMainWindow();
        await this.IncreaseAsync();
    }

    public ValueTask SearchAsync(
        SearchRequest request,
        IWriteOnlyResultList<SearchResult> results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.IsEmptyText)
            return ValueTask.CompletedTask;

        if (!this.TryMatch(request.SearchText!, out var match, out var title))
            return ValueTask.CompletedTask;

        results.Add(new SearchResult(request, this)
        {
            Title = title,
            Description = Description,
            MatchResult = match!.Value,
            Source = "鼠标增强"
        });
        return ValueTask.CompletedTask;
    }

    public ValueTask<IEnumerable<ISearchResultAction>> GetActionAsync(SearchResult searchResult) =>
        ValueTask.FromResult<IEnumerable<ISearchResultAction>>(
        [
            AliasNameAction.Instance,
            HotKeyAction.Instance
        ]);

    private static Dictionary<IAttachEavProperty, object> CreateNameDefaults(MioObject node)
        => new()
        {
            [MioFeatureExtensions.NameProperty] = node.Name!,
            [MioFeatureExtensions.DescriptionProperty] = node.Description!
        };

    public override ValueTask<PluginDataCleanupPlan> CreateDataCleanupPlanAsync(
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PluginDataCleanupPlan
        {
            AdditionalEavRootObjectIds = []
        });
    }
}
