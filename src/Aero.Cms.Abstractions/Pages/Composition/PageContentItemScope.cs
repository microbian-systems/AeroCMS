namespace Aero.Cms.Abstractions.Pages.Composition;

using Aero.Cms.Abstractions.Content.Views;

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

    /// <summary>
    /// Gets an optional provider-qualified virtual entry identity. When present, this scope
    /// resolves through a site-scoped content-entry provider instead of a persisted content item.
    /// </summary>
    public ContentEntryKey? ContentEntryKey { get; init; }

    /// <summary>
    /// Gets the persisted page-route parameter whose value replaces the virtual
    /// entry stable identifier at render time. The provider always remains fixed.
    /// </summary>
    public string? StableIdRouteParameter { get; init; }

    /// <summary>Gets whether this scope targets a provider-qualified virtual entry.</summary>
    public bool IsVirtualEntry => ContentEntryKey is not null;
}
