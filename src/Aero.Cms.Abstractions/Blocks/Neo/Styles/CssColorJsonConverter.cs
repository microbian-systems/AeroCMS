using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

public sealed class CssColorJsonConverter : JsonConverter<CssColor>
{
    public override CssColor Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetString() ?? string.Empty);

    public override void Write(
        Utf8JsonWriter writer,
        CssColor value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
