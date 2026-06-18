namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Concrete record implementation of <see cref="ISlotDefinition"/>.
/// </summary>
public sealed record SlotDefinition(
    string Id,
    string DisplayName,
    IReadOnlySet<NeoPageNodeKind> AllowedChildKinds,
    int MinChildren = 0,
    int? MaxChildren = null,
    bool Required = false) : ISlotDefinition;
