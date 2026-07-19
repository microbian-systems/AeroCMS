using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using System.Globalization;

namespace Aero.Cms.Modules.Content.Rendering;

/// <summary>
/// Resolves a public content type URL (/content/{typeAlias}/{entrySlug}) and renders
/// the matching content item through the existing Scriban pipeline.
/// </summary>
public sealed class ContentTypeUrlRenderer(
    IContentTypeService typeService,
    IContentService contentService,
    IContentItemRenderer itemRenderer)
{
    /// <summary>
    /// Renders a published content item by content type alias and entry slug.
    /// Returns Ok(html) on success, or an AeroError describing what went wrong.
    /// </summary>
    public async Task<Result<PublicContentRenderResult, AeroError>> RenderAsync(
        long siteId,
        string typeAlias,
        string culture,
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
        var normalizedCulture = CultureInfo.GetCultureInfo(culture).Name;
        var itemResult = await contentService.GetBySlugAndTypeAsync(
            siteId,
            typeAlias,
            normalizedCulture,
            entrySlug,
            ct);
        if (itemResult is Result<ContentItem, AeroError>.Failure)
            return AeroError.CreateError($"Entry '{entrySlug}' was not found in '{typeAlias}'.");

        var item = ((Result<ContentItem, AeroError>.Ok)itemResult).Value;
        if (item.PublicationState != ContentPublicationState.Published)
            return AeroError.CreateError("This entry is not published.");

        var htmlResult = await itemRenderer.RenderAsync(type, item, ct);
        return htmlResult switch
        {
            Result<string, AeroError>.Ok html => Prelude.Ok<PublicContentRenderResult, AeroError>(
                new PublicContentRenderResult(html.Value, item.Id, item.Culture)),
            Result<string, AeroError>.Failure failure => failure.Error,
            _ => AeroError.CreateError("Content rendering failed.")
        };
    }
}

/// <summary>
/// Public rendered content plus the stable metadata needed for output-cache tags.
/// </summary>
public sealed record PublicContentRenderResult(string Html, long ItemId, string Culture);
