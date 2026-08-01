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
/// <param name="typeService">The service used to resolve the site-scoped content-type definition.</param>
/// <param name="contentService">The service used to resolve a site-, type-, culture-, and slug-scoped item.</param>
/// <param name="itemRenderer">The renderer that executes the type's Scriban template.</param>
public sealed class ContentTypeUrlRenderer(
    IContentTypeService typeService,
    IContentService contentService,
    IContentItemRenderer itemRenderer)
{
    /// <summary>
    /// Renders a published content item by content type alias and entry slug.
    /// Returns Ok(html) on success, or an AeroError describing what went wrong.
    /// </summary>
    /// <param name="siteId">The site boundary applied to both definition and item lookup.</param>
    /// <param name="typeAlias">The content-type alias used for lookup.</param>
    /// <param name="culture">The culture name normalized through <see cref="CultureInfo"/>.</param>
    /// <param name="entrySlug">The public item slug.</param>
    /// <param name="ct">The token propagated through lookup and rendering.</param>
    /// <returns>Rendered HTML and cache metadata, or a failure describing lookup, policy, or rendering.</returns>
    /// <exception cref="CultureNotFoundException">Thrown when <paramref name="culture"/> is invalid.</exception>
    /// <remarks>
    /// The returned HTML is template output and is not sanitized here; consumers must preserve the
    /// renderer's trust boundary when emitting it as raw HTML.
    /// </remarks>
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
/// <param name="Html">The renderer-produced HTML, which is not sanitized by this record.</param>
/// <param name="ItemId">The globally unique item identifier used for cache tagging.</param>
/// <param name="Culture">The stored item culture used for culture-specific tags.</param>
public sealed record PublicContentRenderResult(string Html, long ItemId, string Culture);
