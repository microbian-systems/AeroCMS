using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Templating;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Rendering;

/// <summary>
/// Renders content items through the configured secure Scriban renderer.
/// </summary>
public sealed class ContentItemRenderer(
    IEnumerable<IFieldTemplateSnippet> templateSnippets,
    ISecureScribanRenderer scribanRenderer) : IContentItemRenderer
{
    /// <inheritdoc />
    /// <remarks>
    /// A default template is generated when the definition has no template; otherwise the
    /// supplied template is normalized. The generated schema is cloned into a render
    /// definition and disposed after rendering. Default site scope contains only the item's
    /// site identifier and culture.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="typeDefinition"/> or <paramref name="item"/> is null.
    /// </exception>
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
