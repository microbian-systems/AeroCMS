namespace Aero.Cms.Abstractions.Blocks.Neo;

using Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// A node in the Neo editor composition tree.
/// Persisted inside <see cref="NeoCompositionBlock"/>.
/// Uses <c>Dictionary&lt;string, JsonElement&gt;</c> for Properties (not <c>JsonObject</c>)
/// for reliable Marten/STJ serialization across all configurations.
/// </summary>
public sealed class NeoPageNode
{
    /// <summary>
    /// Stable client-generated ID used for editor tracking and tree operations.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Stable catalog identifier for this node (e.g., "aero.hero.basic", "ui.heading").
    /// Used to look up rendering, editing, and metadata in <c>NeoEditorCatalog</c>.
    /// </summary>
    public string CatalogId { get; set; } = string.Empty;

    /// <summary>
    /// The kind of node, which determines how it participates in the editor and rendering.
    /// </summary>
    public NeoPageNodeKind Kind { get; set; } = NeoPageNodeKind.Component;

    /// <summary>
    /// Flexible property bag for node configuration.
    /// Uses <c>Dictionary&lt;string, JsonElement&gt;</c> for reliable serialization.
    /// </summary>
    public Dictionary<string, JsonElement> Properties { get; set; } = [];

    /// <summary>
    /// Typed responsive styles shared by all node definitions.
    /// </summary>
    public ResponsiveNodeStyle Style { get; set; } = new();

    /// <summary>
    /// When placed inside a slotted container, identifies which slot this node belongs to.
    /// Null for nodes in non-slotted containers or root nodes.
    /// </summary>
    public string? SlotId { get; set; }

    /// <summary>
    /// Child nodes for composition-capable nodes (Container, Component, Primitive).
    /// </summary>
    public List<NeoPageNode> Children { get; set; } = [];
}
