using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Templating;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Rendering;

/// <summary>
/// Represents a class for ContentItemRenderer.
/// </summary>
public sealed class ContentItemRenderer(
    IEnumerable<IFieldTemplateSnippet> templateSnippets,
    ISecureScribanRenderer scribanRenderer) : IContentItemRenderer
{
        /// <summary>
    /// RenderAsync method.
    /// </summary>
public async Task<Result<string, AeroError>> RenderAsync(
        ContentTypeDefinition typeDefinition,
        ContentItem item,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeDefinition);
        ArgumentNullException.ThrowIfNull(item);

        var template = string.IsNullOrWhiteSpace(typeDefinition.ScribanTemplate)
            ? ContentTypeTemplateGenerator.GenerateTemplate(typeDefinition, templateSnippets)
            : typeDefinition.ScribanTemplate;
        template = ContentTypeTemplateGenerator.NormalizeTemplate(
            template,
            typeDefinition.Fields);

        using var schema = ContentTypeSchemaGenerator.GenerateSchema(typeDefinition);
        using var data = CreateTemplateData(item.Fields);
        var definition = new ScribanRenderDefinition(
            typeDefinition.Id,
            Version: 1,
            template,
            JsonDocument.Parse(schema.RootElement.GetRawText()));
        try
        {
            return await scribanRenderer.RenderAsync(definition, data, ct);
        }
        finally
        {
            definition.DataSchema?.Dispose();
        }
    }

    private static JsonDocument CreateTemplateData(IReadOnlyDictionary<string, JsonElement> fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in fields.OrderBy(static field => field.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray());
    }
}
