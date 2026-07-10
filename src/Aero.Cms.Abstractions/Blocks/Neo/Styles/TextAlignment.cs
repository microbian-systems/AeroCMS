using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Defines an enumeration for TextAlignment.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TextAlignment>))]
public enum TextAlignment
{
    Inherit,
    Start,
    Center,
    End,
    Justify
}
