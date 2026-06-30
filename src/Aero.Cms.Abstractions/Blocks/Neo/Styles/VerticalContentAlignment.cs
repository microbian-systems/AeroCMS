using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

[JsonConverter(typeof(JsonStringEnumConverter<VerticalContentAlignment>))]
public enum VerticalContentAlignment
{
    Inherit,
    Top,
    Middle,
    Bottom,
    Stretch
}
