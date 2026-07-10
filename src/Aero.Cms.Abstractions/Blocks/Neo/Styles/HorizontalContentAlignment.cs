using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Defines an enumeration for HorizontalContentAlignment.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HorizontalContentAlignment>))]
public enum HorizontalContentAlignment
{
    Inherit,
    Start,
    Center,
    End,
    Stretch
}
