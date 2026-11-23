using MouseEnhance.Features;
using MouseEnhance.Services;

namespace MouseEnhance.Nodes;

public abstract class CursorWrapNode : MioObject,
    IMouseEnhanceFeature,
    IAliasNameFeature,
    IHotkeyFeature,
    IResultActionProviderFeature,
    IUseFeature
{
    protected CursorWrapNode(string id) : base(id)
    {
        PreloadPropertySource =
        [
            ..PreloadPropertySource,
            AliasNameExtension.AliasNameProperty,
            HotkeyExtension.HotkeyProperty,
            RecentExtension.UseCountProperty,
            RecentExtension.LastUseTimeProperty
        ];
    }

    protected abstract bool Active { get; }

    public ValueTask<IEnumerable<ISearchResultAction>> GetActionAsync(SearchResult searchResult) =>
        ValueTask.FromResult<IEnumerable<ISearchResultAction>>([AliasNameAction.Instance, HotKeyAction.Instance]);

    public async Task InvokeAsync(InvokeContext context)
    {
        var plugin = this.FindAncestor<MouseEnhancePlugin>()
            ?? throw new InvalidOperationException("鼠标增强节点尚未加载。");
        var services = plugin.Context;
        services.GetRequiredService<CursorWrapController>()
            .SetFeatureActive(Active, settings => plugin.SetCursorWrapSettings(settings));

        if (context.Source == InvokeSource.SearchBox)
            await services.GetRequiredService<ISearchBoxWindow>().HideWindowAsync().ConfigureAwait(false);

        await this.IncreaseAsync();
    }
}

[EavType(MouseEnhanceConst.StartCursorWrapNodeTypeId)]
public sealed class StartCursorWrapNode(string id) : CursorWrapNode(id)
{
    public override Guid MioType => MouseEnhanceConst.StartCursorWrapNodeType;
    protected override bool Active => true;
}

[EavType(MouseEnhanceConst.StopCursorWrapNodeTypeId)]
public sealed class StopCursorWrapNode(string id) : CursorWrapNode(id)
{
    public override Guid MioType => MouseEnhanceConst.StopCursorWrapNodeType;
    protected override bool Active => false;
}
