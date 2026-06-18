namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// A definition that declares named slots (regions) where child nodes can be placed.
/// Slots provide semantic meaning to container children beyond a flat ordered list.
/// </summary>
public interface ISlotted
{
    /// <summary>
    /// The ordered list of slots declared by this container.
    /// </summary>
    IReadOnlyList<ISlotDefinition> Slots { get; }

    /// <summary>
    /// Resolves a slot by its identifier. Returns null if not found.
    /// </summary>
    ISlotDefinition? GetSlot(string slotId);
}

/// <summary>
/// Describes a single named region within a slotted container.
/// </summary>
public interface ISlotDefinition
{
    /// <summary>Unique identifier within the container (e.g., "header", "items", "footer").</summary>
    string Id { get; }

    /// <summary>Human-readable display name (e.g., "Header Content").</summary>
    string DisplayName { get; }

    /// <summary>The kinds of child nodes allowed in this slot.</summary>
    IReadOnlySet<NeoPageNodeKind> AllowedChildKinds { get; }

    /// <summary>Minimum number of children required in this slot.</summary>
    int MinChildren { get; }

    /// <summary>Maximum number of children allowed in this slot. Null = unlimited.</summary>
    int? MaxChildren { get; }

    /// <summary>Whether this slot must have at least MinChildren nodes to be valid.</summary>
    bool Required { get; }
}
