using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Content.Rendering;

/// <summary>
/// Resolves a public content type URL ({typeAlias}/{entrySlug}) and renders
/// the matching content item through the existing Scriban pipeline.
/// </summary>
public sealed class ContentTypeUrlRenderer(
    IContentTypeService typeService,
    IContentService contentService,
    IContentTypeRenderingBridge bridge,
    ISecureScribanRenderer scribanRenderer)
{
    /// <summary>
    /// Renders a published content item by content type alias and entry slug.
    /// Returns Ok(html) on success, or an AeroError describing what went wrong.
    /// </summary>
    public async Task<Result<string, AeroError>> RenderAsync(
        long siteId,
        string typeAlias,
        string entrySlug,
        CancellationToken ct = default)
    {
        // 1. Look up the content type definition
        var typeResult = await typeService.GetByAliasAsync(siteId, typeAlias, ct);
        if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure)
            return AeroError.CreateError($"Content type '{typeAlias}' was not found.");

        var type = ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value;
        if (!type.AllowPublicUrl)
            return AeroError.CreateError($"Content type '{typeAlias}' does not allow public URLs.");

        // 2. Load the published content item by site, type, and slug
        var itemResult = await contentService.GetBySlugAndTypeAsync(siteId, typeAlias, entrySlug, ct);
        if (itemResult is Result<ContentItem, AeroError>.Failure)
            return AeroError.CreateError($"Entry '{entrySlug}' was not found in '{typeAlias}'.");

        var item = ((Result<ContentItem, AeroError>.Ok)itemResult).Value;
        if (item.PublicationState != ContentPublicationState.Published)
            return AeroError.CreateError("This entry is not published.");

        // 3. Bridge to a DynamicTemplateBlock
        var blockResult = await bridge.ToDynamicBlockAsync(type, item, ct);
        if (blockResult is Result<DynamicTemplateBlock, AeroError>.Failure f1)
            return f1.Error;

        var block = ((Result<DynamicTemplateBlock, AeroError>.Ok)blockResult).Value;

        // 4. Resolve the Scriban template definition
        var defResult = await bridge.GetDefinitionAsync(type, ct);
        if (defResult is Result<DynamicBlockDefinition, AeroError>.Failure f2)
            return f2.Error;

        var definition = ((Result<DynamicBlockDefinition, AeroError>.Ok)defResult).Value;

        // 5. Render through Scriban
        var htmlResult = await scribanRenderer.RenderAsync(definition, block.Data, ct);
        if (htmlResult is Result<string, AeroError>.Ok htmlOk)
            return htmlOk.Value;

        if (htmlResult is Result<string, AeroError>.Failure f3)
            return f3.Error;

        return AeroError.CreateError("Unexpected rendering result.");
    }
}
