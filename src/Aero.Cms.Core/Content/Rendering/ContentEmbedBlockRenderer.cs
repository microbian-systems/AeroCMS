using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Core.Content.Rendering;

/// <summary>
/// Renders a ContentEmbedBlock by bridging to the content type rendering pipeline.
/// Loads the referenced ContentItem, resolves its ContentTypeDefinition, bridges to
/// a DynamicTemplateBlock, and renders through Scriban.
/// </summary>
public sealed class ContentEmbedBlockRenderer(
    IContentService contentService,
    IContentTypeService typeService,
    IContentItemRenderer itemRenderer,
    ILogger<ContentEmbedBlockRenderer> logger)
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
        {
            logger.LogWarning("Content embed item {ContentItemId} could not be loaded.", block.ContentItemId);
            return string.Empty;
        }

        // 2. Load the ContentTypeDefinition
        var typeResult = await typeService.GetByAliasAsync(itemOk.Value.SiteId, itemOk.Value.ContentTypeAlias, ct);
        if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
        {
            logger.LogWarning(
                "Content type {ContentTypeAlias} for embedded item {ContentItemId} could not be loaded.",
                itemOk.Value.ContentTypeAlias,
                itemOk.Value.Id);
            return string.Empty;
        }

        var htmlResult = await itemRenderer.RenderAsync(typeOk.Value, itemOk.Value, ct);
        if (htmlResult is Result<string, AeroError>.Ok htmlOk)
            return htmlOk.Value;

        if (htmlResult is Result<string, AeroError>.Failure failure)
        {
            logger.LogError(
                "Rendering embedded content item {ContentItemId} failed: {Error}",
                itemOk.Value.Id,
                failure.Error);
        }

        return string.Empty;
    }
}
