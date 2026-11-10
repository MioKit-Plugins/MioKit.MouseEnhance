using MouseEnhance.Features;
using MouseEnhance.Services;

namespace MouseEnhance.Nodes;

[EavType(MouseEnhanceConst.StartNodeTypeId)]
public sealed class StartMouseEnhanceNode : MioObject,
    IMouseEnhanceFeature,
    IAliasNameFeature,
    IHotkeyFeature,
    IResultActionProviderFeature,
    IUseFeature
{
    public StartMouseEnhanceNode(string id) : base(id)
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

    public override Guid MioType => MouseEnhanceConst.StartNodeType;

    public ValueTask<IEnumerable<ISearchResultAction>> GetActionAsync(SearchResult searchResult) =>
        ValueTask.FromResult<IEnumerable<ISearchResultAction>>(
        [
            AliasNameAction.Instance,
            HotKeyAction.Instance
        ]);

    public async Task InvokeAsync(InvokeContext context)
    {
        MouseEnhanceRegister.Instance.ComponentContext
            .Resolve<MouseEnhanceService>()
            .EnableWindowDrag();

        if (context.Source == InvokeSource.SearchBox)
            await MioIoc.Resolve<ISearchBoxWindow>().HideWindowAsync().ConfigureAwait(false);

        await this.IncreaseAsync();
    }
}
