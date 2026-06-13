using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Defines the kind of a <see cref="NeoPageNode"/> in the Neo composition tree.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NeoPageNodeKind
{
    /// <summary>Represents a typed block (e.g., Hero01Block).</summary>
    Block,

    /// <summary>Represents a page section region.</summary>
    Section,

    /// <summary>Represents a layout container that groups children.</summary>
    Container,

    /// <summary>Represents a reusable component instance from the catalog.</summary>
    Component,

    /// <summary>Represents a UI primitive (e.g., heading, paragraph, button).</summary>
    Primitive,
}
