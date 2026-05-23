using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Rendering;

/// <summary>
/// Renders a ContentEmbedBlock by bridging to the content type rendering pipeline.
/// Loads the referenced ContentItem, resolves its ContentTypeDefinition, bridges to
/// a DynamicTemplateBlock, and renders through Scriban.
/// </summary>
public sealed class ContentEmbedBlockRenderer(
    IContentService contentService,
    IContentTypeService typeService,
    IContentTypeRenderingBridge bridge,
    ISecureScribanRenderer scribanRenderer)
{
    /// <summary>
    /// Renders the content item referenced by the given embed block.
    /// </summary>
    /// <param name="block">The content embed block containing the content item ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>HTML string, or empty string on failure.</returns>
    public async Task<string> RenderAsync(ContentEmbedBlock block, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(block);

        // 1. Load the ContentItem by ID
        var itemResult = await contentService.LoadAsync(block.ContentItemId, ct);
        if (itemResult is not Result<ContentItem, AeroError>.Ok itemOk)
            return string.Empty;

        // 2. Load the ContentTypeDefinition
        var typeResult = await typeService.GetByAliasAsync(itemOk.Value.SiteId, itemOk.Value.ContentTypeAlias, ct);
        if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            return string.Empty;

        // 3. Bridge to DynamicTemplateBlock
        var blockResult = await bridge.ToDynamicBlockAsync(typeOk.Value, itemOk.Value, ct);
        if (blockResult is not Result<DynamicTemplateBlock, AeroError>.Ok blockOk)
            return string.Empty;

        // 4. Get the DynamicBlockDefinition (for the Scriban template)
        var defResult = await bridge.GetDefinitionAsync(typeOk.Value, ct);
        if (defResult is not Result<DynamicBlockDefinition, AeroError>.Ok defOk)
            return string.Empty;

        // 5. Render through Scriban
        var htmlResult = await scribanRenderer.RenderAsync(defOk.Value, blockOk.Value.Data, ct);
        if (htmlResult is Result<string, AeroError>.Ok htmlOk)
            return htmlOk.Value;

        return string.Empty;
    }
}
