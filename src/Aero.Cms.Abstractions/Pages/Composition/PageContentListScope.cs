namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Repeats a template subtree over one pageable content-type query.
/// </summary>
public sealed record PageContentListScope
{
    /// <summary>Gets the container node that owns this scope.</summary>
    public long NodeId { get; init; }

    /// <summary>Gets the stable content-type identifier.</summary>
    public long ContentTypeId { get; init; }

    /// <summary>Gets the last-known content-type alias for authoring diagnostics.</summary>
    public string ContentTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the subtree cloned once for every resolved content item.</summary>
    public long TemplateRootNodeId { get; init; }

    /// <summary>Gets the persisted query definition.</summary>
    public PageContentListQuery Query { get; init; } = new();

    /// <summary>Gets the behavior used when the query returns no entries.</summary>
    public PageContentEmptyStateBehavior EmptyState { get; init; } =
        PageContentEmptyStateBehavior.RenderNothing;

    /// <summary>Creates an independent copy of this list scope.</summary>
    /// <returns>A copy with independent filter storage.</returns>
    public PageContentListScope CreateSnapshot() => this with
    {
        Query = Query?.CreateSnapshot() ?? new PageContentListQuery()
    };
}
