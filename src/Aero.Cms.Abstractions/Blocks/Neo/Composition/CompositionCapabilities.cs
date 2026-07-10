namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Default immutable implementation of <see cref="ICompositionCapabilities"/>.
/// </summary>
public sealed record CompositionCapabilities(
    bool IsEmbeddable,
    bool CanContainChildren,
    IReadOnlySet<NeoPageNodeKind> AllowedChildKinds,
    IReadOnlySet<NeoPageNodeKind> AllowedParentKinds,
    int? MaximumChildren,
    IReadOnlyList<NeoDropZoneDefinition> SupportedDropZones,
    IReadOnlySet<string>? AllowedChildCatalogIds = null,
    IReadOnlySet<string>? AllowedParentCatalogIds = null,
    bool IsSlotted = false) : ICompositionCapabilities
{
        /// <summary>
    /// Leaf method.
    /// </summary>
public static CompositionCapabilities Leaf(params NeoPageNodeKind[] allowedParentKinds) =>
        new(
            true,
            false,
            EmptyKinds,
            allowedParentKinds.ToHashSet(),
            0,
            []);

        /// <summary>
    /// Container method.
    /// </summary>
public static CompositionCapabilities Container(
        IEnumerable<NeoPageNodeKind> allowedChildKinds,
        IEnumerable<NeoPageNodeKind> allowedParentKinds,
        int? maximumChildren = null,
        IReadOnlyList<NeoDropZoneDefinition>? dropZones = null,
        IReadOnlySet<string>? allowedChildCatalogIds = null,
        IReadOnlySet<string>? allowedParentCatalogIds = null,
        bool isSlotted = false)
    {
        var childKinds = allowedChildKinds.ToHashSet();

        return new CompositionCapabilities(
            true,
            true,
            childKinds,
            allowedParentKinds.ToHashSet(),
            maximumChildren,
            dropZones ??
            [
                new NeoDropZoneDefinition(
                    NeoDropZoneDefinition.DefaultId,
                    childKinds,
                    maximumChildren)
            ],
            allowedChildCatalogIds,
            allowedParentCatalogIds,
            IsSlotted: isSlotted);
    }

    private static IReadOnlySet<NeoPageNodeKind> EmptyKinds { get; } =
        new HashSet<NeoPageNodeKind>();
}
