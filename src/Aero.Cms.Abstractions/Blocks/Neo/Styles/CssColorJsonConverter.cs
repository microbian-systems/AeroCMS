using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Represents a class for CssColorJsonConverter.
/// </summary>
public sealed class CssColorJsonConverter : JsonConverter<CssColor>
{
        /// <summary>
    /// Read method.
    /// </summary>
public override CssColor Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetString() ?? string.Empty);

        /// <summary>
    /// Write method.
    /// </summary>
public override void Write(
        Utf8JsonWriter writer,
        CssColor value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
