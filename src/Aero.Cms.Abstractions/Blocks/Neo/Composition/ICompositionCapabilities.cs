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
}
