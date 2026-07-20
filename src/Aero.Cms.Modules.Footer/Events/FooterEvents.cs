using Aero.Cms.Modules.Footer.Domain;

namespace Aero.Cms.Modules.Footer.Events;

/// <summary>
/// Establishes a footer stream and its site, culture, translation-group, and authoring metadata.
/// </summary>
/// <param name="SiteId">The owning site identifier.</param>
/// <param name="Name">The author-facing footer name.</param>
/// <param name="Key">The site-and-culture-scoped footer key.</param>
/// <param name="Description">The optional author-facing description.</param>
/// <param name="UserId">The optional identifier of the creating user.</param>
/// <param name="CreatedOn">The creation timestamp.</param>
/// <param name="Culture">The culture represented by this footer stream.</param>
/// <param name="TranslationGroupId">The optional identifier shared by culture variants.</param>
public sealed record FooterCreated(
    long SiteId,
    string Name,
    string Key,
    string? Description,
    long? UserId,
    DateTimeOffset CreatedOn,
    string Culture = "en-US",
    long? TranslationGroupId = null);

/// <summary>
/// Replaces the current editor draft and updates the footer's authoring metadata.
/// </summary>
/// <param name="SiteId">The owning site identifier recorded with the event.</param>
/// <param name="Name">The updated author-facing name.</param>
/// <param name="Key">The updated site-and-culture-scoped key.</param>
/// <param name="Description">The updated optional description.</param>
/// <param name="Snapshot">The complete draft snapshot.</param>
/// <param name="UserId">The optional identifier of the saving user.</param>
/// <param name="SavedOn">The save timestamp.</param>
/// <param name="ChangeNote">An optional author-supplied description of the change.</param>
public sealed record FooterDraftSaved(
    long SiteId,
    string Name,
    string Key,
    string? Description,
    FooterSnapshot Snapshot,
    long? UserId,
    DateTimeOffset SavedOn,
    string? ChangeNote);

/// <summary>
/// Captures the immutable snapshot selected for publication.
/// </summary>
/// <param name="SiteId">The owning site identifier recorded with the event.</param>
/// <param name="Snapshot">The complete published snapshot.</param>
/// <param name="UserId">The optional identifier of the publishing user.</param>
/// <param name="PublishedOn">The publication timestamp.</param>
/// <param name="ChangeNote">An optional author-supplied description of the publication.</param>
public sealed record FooterPublished(
    long SiteId,
    FooterSnapshot Snapshot,
    long? UserId,
    DateTimeOffset PublishedOn,
    string? ChangeNote);

/// <summary>
/// Marks a footer unavailable for normal authoring lists and public resolution.
/// </summary>
/// <param name="SiteId">The owning site identifier recorded with the event.</param>
/// <param name="UserId">The optional identifier of the archiving user.</param>
/// <param name="ArchivedOn">The archive timestamp.</param>
public sealed record FooterArchived(
    long SiteId,
    long? UserId,
    DateTimeOffset ArchivedOn);

/// <summary>
/// Selects or clears the default footer for a site.
/// </summary>
/// <param name="SiteId">The site whose default changes.</param>
/// <param name="FooterId">The selected footer identifier, or <see langword="null"/> to clear the selection.</param>
/// <param name="UserId">The optional identifier of the user making the change.</param>
/// <param name="ChangedOn">The change timestamp.</param>
public sealed record SiteDefaultFooterChanged(
    long SiteId,
    long? FooterId,
    long? UserId,
    DateTimeOffset ChangedOn);

/// <summary>
/// Creates and parses the stable string keys used by footer event streams.
/// </summary>
public static class FooterStreams
{
    /// <summary>
    /// Creates the event-stream key for a footer identifier.
    /// </summary>
    /// <param name="id">The footer identifier.</param>
    /// <returns>A key in the form <c>footer-{id}</c>.</returns>
    public static string Footer(long id) => $"footer-{id}";

    /// <summary>
    /// Creates the event-stream key for a site's footer settings.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <returns>A key in the form <c>site-footer-settings-{siteId}</c>.</returns>
    public static string SiteSettings(long siteId) => $"site-footer-settings-{siteId}";

    /// <summary>
    /// Determines whether a key has the footer-stream prefix.
    /// </summary>
    /// <param name="streamKey">The key to inspect.</param>
    /// <returns><see langword="true"/> when the key starts with <c>footer-</c>; otherwise, <see langword="false"/>.</returns>
    /// <remarks>This prefix check does not verify that the suffix is a valid integer.</remarks>
    public static bool IsFooterStream(string? streamKey)
        => streamKey?.StartsWith("footer-", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Determines whether a key has the site-footer-settings stream prefix.
    /// </summary>
    /// <param name="streamKey">The key to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when the key starts with <c>site-footer-settings-</c>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>This prefix check does not verify that the suffix is a valid integer.</remarks>
    public static bool IsSiteSettingsStream(string? streamKey)
        => streamKey?.StartsWith("site-footer-settings-", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Extracts the footer identifier from a footer stream key.
    /// </summary>
    /// <param name="streamKey">A key in the form <c>footer-{id}</c>.</param>
    /// <returns>The parsed footer identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the prefix or integer suffix is invalid.</exception>
    public static long ExtractFooterId(string streamKey)
        => ExtractLongId(streamKey, "footer-");

    /// <summary>
    /// Extracts the site identifier from a site-footer-settings stream key.
    /// </summary>
    /// <param name="streamKey">A key in the form <c>site-footer-settings-{id}</c>.</param>
    /// <returns>The parsed site identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the prefix or integer suffix is invalid.</exception>
    public static long ExtractSiteId(string streamKey)
        => ExtractLongId(streamKey, "site-footer-settings-");

    private static long ExtractLongId(string streamKey, string prefix)
    {
        if (streamKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(streamKey.AsSpan(prefix.Length), out var id))
        {
            return id;
        }

        throw new InvalidOperationException(
            $"Cannot extract long ID from stream key '{streamKey}'. Expected format '{prefix}{{id}}'.");
    }
}
