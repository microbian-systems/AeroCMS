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
        var model = ScribanContentRenderModel.Create(typeDefinition, item);
        var definition = new ScribanRenderDefinition(
            typeDefinition.Id,
            Version: 1,
            template,
            JsonDocument.Parse(schema.RootElement.GetRawText()));
        try
        {
            return await scribanRenderer.RenderAsync(definition, model, ct);
        }
        finally
        {
            definition.DataSchema?.Dispose();
        }
    }
}
