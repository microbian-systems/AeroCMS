namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Immutable catalog-level rules that describe how a node participates in composition.
/// </summary>
public interface ICompositionCapabilities
{
        /// <summary>
    /// Gets or sets the Is Embeddable.
    /// </summary>
bool IsEmbeddable { get; }

        /// <summary>
    /// Gets or sets the Can Contain Children.
    /// </summary>
bool CanContainChildren { get; }

        /// <summary>
    /// Gets or sets the Allowed Child Kinds.
    /// </summary>
IReadOnlySet<NeoPageNodeKind> AllowedChildKinds { get; }

        /// <summary>
    /// Gets or sets the Allowed Parent Kinds.
    /// </summary>
IReadOnlySet<NeoPageNodeKind> AllowedParentKinds { get; }

        /// <summary>
    /// Gets or sets the Maximum Children.
    /// </summary>
int? MaximumChildren { get; }

        /// <summary>
    /// Gets or sets the Supported Drop Zones.
    /// </summary>
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
