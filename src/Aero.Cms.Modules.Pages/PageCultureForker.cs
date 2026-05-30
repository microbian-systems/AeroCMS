using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Pages;

public static class PageCultureForker
{
    public static PageDocument Fork(PageDocument source, long targetPageId, string targetCulture, string targetSlug)
    {
        ArgumentNullException.ThrowIfNull(source);

        var normalizedSlug = targetSlug.Trim().Trim('/');
        var translationSetId = source.TranslationSetId ?? source.Id;

        return new PageDocument
        {
            Id = targetPageId,
            SiteId = source.SiteId,
            TranslationSetId = translationSetId,
            Culture = ContentSlugDocument.NormalizeCulture(targetCulture),
            Kind = source.Kind,
            Slug = normalizedSlug,
            Title = source.Title,
            Summary = source.Summary,
            SeoTitle = source.SeoTitle,
            SeoDescription = source.SeoDescription,
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
            BlockSchemaVersion = source.BlockSchemaVersion,
            Blocks = source.Blocks.Select(x => x.DeepClone()).ToList(),
            LayoutRegions = [],
            BlockIdMap = []
        };
    }
}
