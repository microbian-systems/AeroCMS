namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Immutable catalog-level rules that describe how a node participates in composition.
/// </summary>
public interface ICompositionCapabilities
{
    bool IsEmbeddable { get; }

    bool CanContainChildren { get; }

    IReadOnlySet<NeoPageNodeKind> AllowedChildKinds { get; }

    IReadOnlySet<NeoPageNodeKind> AllowedParentKinds { get; }

    int? MaximumChildren { get; }

    IReadOnlyList<NeoDropZoneDefinition> SupportedDropZones { get; }

    /// <summary>
    /// When true, the definition implements <see cref="ISlotted"/> with at least one slot.
    /// </summary>
    bool IsSlotted { get; }

    /// <summary>
    /// Optional set of specific CatalogIds that are allowed as children.
    /// When null (default), only <see cref="AllowedChildKinds"/> kind-level
    /// enforcement applies. When non-null, a child must match both its Kind
    /// AND its CatalogId to be accepted.
    /// </summary>
    IReadOnlySet<string>? AllowedChildCatalogIds { get; }

    /// <summary>
    /// Optional set of specific CatalogIds that are allowed as parents.
    /// When null (default), only <see cref="AllowedParentKinds"/> kind-level
    /// enforcement applies. When non-null, a parent must match both its Kind
    /// AND its CatalogId to be accepted.
    /// </summary>
    IReadOnlySet<string>? AllowedParentCatalogIds { get; }
}
