using System.Globalization;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;

namespace Aero.Cms.Modules.Footer;

public sealed record FooterCultureFork(
    FooterCreated Created,
    FooterDraftSaved DraftSaved);

public static class FooterCultureForker
{
    public static FooterCultureFork Fork(
        FooterDocument source,
        FooterSnapshot sourceSnapshot,
        long targetFooterId,
        string targetCulture,
        long? userId = null,
        DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceSnapshot);

        var forkedOn = timestamp ?? DateTimeOffset.UtcNow;
        var translationSetId = source.TranslationSetId ?? source.Id;
        var culture = NormalizeCulture(targetCulture);
        var snapshot = CloneSnapshot(sourceSnapshot);

        var created = new FooterCreated(
            source.SiteId,
            source.Name,
            source.Key,
            source.Description,
            userId,
            forkedOn,
            culture,
            translationSetId);

        var draftSaved = new FooterDraftSaved(
            source.SiteId,
            source.Name,
            source.Key,
            source.Description,
            snapshot,
            userId,
            forkedOn,
            $"Forked from {source.Culture} to {culture}");

        return new FooterCultureFork(created, draftSaved);
    }

    private static FooterSnapshot CloneSnapshot(FooterSnapshot snapshot)
        => snapshot with
        {
            Legal = snapshot.Legal with { LegalLinks = snapshot.Legal.LegalLinks.Select(CloneLink).ToList() },
            Sections = snapshot.Sections.Select(CloneComponent).ToList()
        };

    private static IFooterComponent CloneComponent(IFooterComponent component)
        => component switch
        {
            FooterLinkGroup group => group with { Links = group.Links.Select(CloneLink).ToList() },
            FooterTextBlock text => text with { },
            FooterSocialLinks social => social with { Links = social.Links.Select(CloneSocialLink).ToList() },
            FooterNewsletterSignup newsletter => newsletter with { },
            FooterSearch search => search with { },
            FooterSpacer spacer => spacer with { },
            _ => component
        };

    private static FooterLink CloneLink(FooterLink link)
        => link with { };

    private static FooterSocialLink CloneSocialLink(FooterSocialLink link)
        => link with { };

    private static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "en-US";
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return "en-US";
        }
    }
}
