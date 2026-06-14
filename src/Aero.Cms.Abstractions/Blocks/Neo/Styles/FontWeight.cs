using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

[JsonConverter(typeof(JsonStringEnumConverter<FontWeight>))]
public enum FontWeight
{
    Inherit = 0,
    Light = 300,
    Normal = 400,
    Medium = 500,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800
}
