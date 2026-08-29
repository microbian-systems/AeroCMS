using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Creates an independent draft page for another culture from an existing page.
/// </summary>
public static class PageCultureForker
{
    /// <summary>
    /// Copies the source page's descriptive fields and draft HTML into a new,
    /// root-level draft in the normalized target culture.
    /// </summary>
    /// <param name="source">The page whose draft content and presentation settings are copied.</param>
    /// <param name="targetPageId">The identifier to assign to the new page.</param>
    /// <param name="targetCulture">
    /// The target culture. Invalid or blank values normalize to the site's default culture.
    /// </param>
    /// <param name="targetSlug">The target slug; surrounding whitespace and slashes are removed.</param>
    /// <returns>
    /// A new draft page linked to the source translation group. The returned page has no parent or
    /// published snapshot, and its HTML tree is cloned while retaining node identifiers.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
public static PageDocument Fork(PageDocument source, long targetPageId, string targetCulture, string targetSlug)
    {
        ArgumentNullException.ThrowIfNull(source);

        var normalizedSlug = targetSlug.Trim().Trim('/');
        var translationGroupId = source.TranslationGroupId ?? source.Id;

        return new PageDocument
        {
            Id = targetPageId,
            SiteId = source.SiteId,
            TranslationGroupId = translationGroupId,
            SourcePageId = source.Id,
            Culture = ContentSlugDocument.NormalizeCulture(targetCulture),
            Kind = source.Kind,
            RendererId = source.RendererId,
            DraftSourceVersionId = null,
            PublishedSourceVersionId = null,
            Slug = normalizedSlug,
            DraftRouteTemplate = source.DraftRouteTemplate,
            Title = source.Title,
            Summary = source.Summary,
            SeoTitle = source.SeoTitle,
            SeoDescription = source.SeoDescription,
            IncludeInSearch = source.IncludeInSearch,
            IncludeInPublicAi = source.IncludeInPublicAi,
            ParentId = null,
            Path = "/" + normalizedSlug,
            Depth = 0,
            Order = 0,
            IsHidden = source.IsHidden,
            PublicationState = ContentPublicationState.Draft,
            ShowInNavMenu = source.ShowInNavMenu,
            ShowHeaderNavigation = source.ShowHeaderNavigation,
            HeaderImageUrl = source.HeaderImageUrl,
            HideHeader = source.HideHeader,
            HideFooter = source.HideFooter,
            ShowChatAgent = source.ShowChatAgent,
            DraftContent = HtmlTreeOperations.ClonePreservingNodeIds(source.DraftContent),
            DraftComposition = source.DraftComposition.CreateSnapshot(),
            PublishedContent = null,
            PublishedComposition = null,
            ContentRevision = source.ContentRevision
        };
    }
}
