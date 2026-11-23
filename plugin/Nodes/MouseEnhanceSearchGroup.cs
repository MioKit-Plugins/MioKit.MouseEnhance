using MouseEnhance.Features;

namespace MouseEnhance.Nodes;

[EavType(MouseEnhanceConst.SearchGroupTypeId)]
public sealed class MouseEnhanceSearchGroup : MioObject, ISearchableFeature
{
    public MouseEnhanceSearchGroup(string id) : base(id)
    {
    }

    public override Guid MioType => MouseEnhanceConst.SearchGroupType;

    public override async Task EnsureSubTreeLoadedAsync()
    {
        await EnsureTreeLoadedAsync<StartMouseEnhanceNode>(MouseEnhanceConst.StartNodeId);
        await EnsureTreeLoadedAsync<StopMouseEnhanceNode>(MouseEnhanceConst.StopNodeId);
        await EnsureTreeLoadedAsync<StartCursorWrapNode>(MouseEnhanceConst.StartCursorWrapNodeId);
        await EnsureTreeLoadedAsync<StopCursorWrapNode>(MouseEnhanceConst.StopCursorWrapNodeId);
    }

    public ValueTask SearchAsync(
        SearchRequest request,
        IWriteOnlyResultList<SearchResult> results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.IsEmptyText)
            return ValueTask.CompletedTask;

        foreach (var node in this.GetDescendants<MioObject>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is not (IMouseEnhanceFeature and IInvokeFeature))
                continue;
            if (!node.TryMatch(request.SearchText!, out var match, out var title))
                continue;

            results.Add(new SearchResult(request, node)
            {
                Title = title,
                Description = node.GetDescription(),
                MatchResult = match!.Value,
                Source = "鼠标增强"
            });
        }

        return ValueTask.CompletedTask;
    }
}
