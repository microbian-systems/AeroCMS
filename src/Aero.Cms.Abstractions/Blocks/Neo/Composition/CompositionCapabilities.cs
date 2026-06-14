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
    IReadOnlyList<NeoDropZoneDefinition> SupportedDropZones) : ICompositionCapabilities
{
    public static CompositionCapabilities Leaf(params NeoPageNodeKind[] allowedParentKinds) =>
        new(
            true,
            false,
            EmptyKinds,
            allowedParentKinds.ToHashSet(),
            0,
            []);

    public static CompositionCapabilities Container(
        IEnumerable<NeoPageNodeKind> allowedChildKinds,
        IEnumerable<NeoPageNodeKind> allowedParentKinds,
        int? maximumChildren = null,
        IReadOnlyList<NeoDropZoneDefinition>? dropZones = null)
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
            ]);
    }

    private static IReadOnlySet<NeoPageNodeKind> EmptyKinds { get; } =
        new HashSet<NeoPageNodeKind>();
}
