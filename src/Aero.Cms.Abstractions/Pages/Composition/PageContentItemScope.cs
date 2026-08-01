namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Resolves one content item for bindings within a page subtree.
/// </summary>
public sealed record PageContentItemScope
{
    /// <summary>Gets the container node that owns this scope.</summary>
    public long NodeId { get; init; }

    /// <summary>Gets the stable content-type identifier.</summary>
    public long ContentTypeId { get; init; }

    /// <summary>Gets the last-known content-type alias for authoring diagnostics.</summary>
    public string ContentTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets whether the item is resolved by stable ID or explicit slug routing.</summary>
    public PageContentItemLookupMode LookupMode { get; init; } =
        PageContentItemLookupMode.StableId;

    /// <summary>Gets the stable item identifier used by the default lookup mode.</summary>
    public long? ContentItemId { get; init; }

    /// <summary>
    /// Gets the routing slug or last-known slug retained for authoring diagnostics.
    /// </summary>
    public string? Slug { get; init; }
}
