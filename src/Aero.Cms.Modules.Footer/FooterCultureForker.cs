using System.Globalization;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;

namespace Aero.Cms.Modules.Footer;

/// <summary>
/// Contains the creation and initial-draft events needed to start a translated footer stream.
/// </summary>
/// <param name="Created">The event that establishes the translated footer identity and culture.</param>
/// <param name="DraftSaved">The event that stores the cloned editor snapshot.</param>
public sealed record FooterCultureFork(
    FooterCreated Created,
    FooterDraftSaved DraftSaved);

/// <summary>
/// Creates detached event payloads for a culture variant of an existing footer.
/// </summary>
public static class FooterCultureForker
{
    /// <summary>
    /// Clones the supplied snapshot and creates the initial events for a culture variant.
    /// </summary>
    /// <param name="source">The source footer whose site, key, metadata, and translation group are retained.</param>
    /// <param name="sourceSnapshot">The editor snapshot to clone.</param>
    /// <param name="targetFooterId">
    /// The identifier reserved for the target footer. The current event payloads do not embed this value;
    /// the caller selects the target stream when persisting them.
    /// </param>
    /// <param name="targetCulture">
    /// The requested culture. Invalid or blank values fall back to <c>en-US</c>.
    /// </param>
    /// <param name="userId">The optional actor recorded on both generated events.</param>
    /// <param name="timestamp">The event timestamp, or UTC now when omitted.</param>
    /// <returns>Two unpersisted events for creating and saving the target draft.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/> or <paramref name="sourceSnapshot"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Known component records and their lists are copied. An unknown <see cref="IFooterComponent"/>
    /// implementation is retained by reference. This method does not validate or persist the cloned snapshot.
    /// </remarks>
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
        var TranslationGroupId = source.TranslationGroupId ?? source.Id;
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
            TranslationGroupId);

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
            Rows = snapshot.Rows.Select(CloneRow).ToList(),
            Sections = snapshot.Sections.Select(CloneComponent).ToList()
        };

    private static FooterCanvasRow CloneRow(FooterCanvasRow row)
        => row with
        {
            Columns = row.Columns.Select(CloneColumn).ToList()
        };

    private static FooterCanvasColumn CloneColumn(FooterCanvasColumn column)
        => column with
        {
            Blocks = column.Blocks.Select(CloneBlock).ToList()
        };

    private static FooterCanvasBlock CloneBlock(FooterCanvasBlock block)
        => block with
        {
            Component = CloneComponent(block.Component)
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
