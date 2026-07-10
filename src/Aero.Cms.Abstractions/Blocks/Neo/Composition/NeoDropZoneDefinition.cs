namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Describes a named child insertion point exposed by a composition-capable node.
/// </summary>
public sealed record NeoDropZoneDefinition(
    string Id,
    IReadOnlySet<NeoPageNodeKind> AllowedChildKinds,
    int? MaximumChildren = null)
{
        /// <summary>
    /// DefaultId.
    /// </summary>
public const string DefaultId = "default";
}
