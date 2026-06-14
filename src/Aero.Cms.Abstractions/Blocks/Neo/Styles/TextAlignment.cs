using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

[JsonConverter(typeof(JsonStringEnumConverter<TextAlignment>))]
public enum TextAlignment
{
    Inherit,
    Start,
    Center,
    End,
    Justify
}
