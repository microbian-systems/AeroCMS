namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Describes a requested insertion or move within a composition tree.
/// </summary>
public sealed record CompositionDropRequest(
    NeoPageNode Node,
    string? ParentNodeId,
    string DropZoneId,
    int TargetIndex);
