using MouseEnhance.Features;
using MouseEnhance.Services;

namespace MouseEnhance.Nodes;

[EavType(MouseEnhanceConst.StopNodeTypeId)]
public sealed class StopMouseEnhanceNode : MioObject,
    IMouseEnhanceFeature,
    IAliasNameFeature,
    IHotkeyFeature,
    IResultActionProviderFeature,
    IUseFeature
{
    public StopMouseEnhanceNode(string id) : base(id)
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

    public override Guid MioType => MouseEnhanceConst.StopNodeType;

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
            .DisableWindowDrag();

        if (context.Source == InvokeSource.SearchBox)
            await MioIoc.Resolve<ISearchBoxWindow>().HideWindowAsync().ConfigureAwait(false);

        await this.IncreaseAsync();
    }
}
