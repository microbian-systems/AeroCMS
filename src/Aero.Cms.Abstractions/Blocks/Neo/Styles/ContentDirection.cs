using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Controls the writing direction for a node and its descendants.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentDirection
{
    Inherit,
    LeftToRight,
    RightToLeft
}
