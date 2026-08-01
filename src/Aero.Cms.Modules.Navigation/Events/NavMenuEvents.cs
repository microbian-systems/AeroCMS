using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Events;

/// <summary>
/// Starts a navigation stream and establishes its site, culture, translation group, and normalized identity metadata.
/// </summary>
/// <param name="SiteId">The owning site identifier.</param>
/// <param name="Name">The initial editor-facing name.</param>
/// <param name="Key">The key normalized by the document projection.</param>
/// <param name="UserId">The optional creating actor.</param>
/// <param name="CreatedOn">The creation timestamp.</param>
/// <param name="Culture">The canonical culture represented by this stream.</param>
/// <param name="TranslationGroupId">The identifier shared by translated variants.</param>
public sealed record NavMenuCreated(
    long SiteId,
    string Name,
    string Key,
    long? UserId,
    DateTimeOffset CreatedOn,
    string Culture = "en-US",
    long? TranslationGroupId = null);

/// <summary>
/// Captures a complete editable snapshot appended to a navigation stream.
/// </summary>
/// <param name="SiteId">The owning site identifier recorded with the event.</param>
/// <param name="Name">The editor-facing name at save time.</param>
/// <param name="Key">The navigation key at save time.</param>
/// <param name="Snapshot">The validated editable component tree.</param>
/// <param name="UserId">The optional saving actor.</param>
/// <param name="SavedOn">The save timestamp.</param>
/// <param name="ChangeNote">An optional description of the draft change.</param>
public sealed record NavMenuDraftSaved(
    long SiteId,
    string Name,
    string Key,
    NavMenuSnapshot Snapshot,
    long? UserId,
    DateTimeOffset SavedOn,
    string? ChangeNote);

/// <summary>
/// Freezes a validated navigation snapshot as the stream's latest public version.
/// </summary>
/// <param name="SiteId">The owning site identifier recorded with the event.</param>
/// <param name="Snapshot">The published component tree.</param>
/// <param name="UserId">The optional publishing actor.</param>
/// <param name="PublishedOn">The publication timestamp.</param>
/// <param name="ChangeNote">The note carried forward from the published draft.</param>
public sealed record NavMenuPublished(
    long SiteId,
    NavMenuSnapshot Snapshot,
    long? UserId,
    DateTimeOffset PublishedOn,
    string? ChangeNote);

/// <summary>
/// Marks a navigation stream as unavailable for active selection and publication reads.
/// </summary>
/// <param name="SiteId">The owning site identifier recorded with the event.</param>
/// <param name="UserId">The optional archiving actor.</param>
/// <param name="ArchivedOn">The archive timestamp.</param>
public sealed record NavMenuArchived(
    long SiteId,
    long? UserId,
    DateTimeOffset ArchivedOn);

/// <summary>
/// Selects or clears the navigation menu used as a site's default.
/// </summary>
/// <param name="SiteId">The site whose default is changing.</param>
/// <param name="NavMenuId">The selected menu identifier, or <see langword="null"/> to clear it.</param>
/// <param name="UserId">The optional actor making the change.</param>
/// <param name="ChangedOn">The change timestamp.</param>
public sealed record SiteDefaultNavMenuChanged(
    long SiteId,
    long? NavMenuId,
    long? UserId,
    DateTimeOffset ChangedOn);

/// <summary>
/// Defines stable event-stream keys and parses their Snowflake identifiers.
/// </summary>
public static class NavMenuStreams
{
    /// <summary>
    /// Builds the event-stream key for a navigation menu.
    /// </summary>
    /// <param name="id">The navigation menu identifier.</param>
    /// <returns>A key in the <c>nav-menu-{id}</c> namespace.</returns>
public static string Menu(long id) => $"nav-menu-{id}";
    /// <summary>
    /// Builds the event-stream key for a site's navigation settings.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <returns>A key in the <c>site-nav-settings-{siteId}</c> namespace.</returns>
public static string SiteSettings(long siteId) => $"site-nav-settings-{siteId}";

    /// <summary>
    /// Determines whether a stream key uses the navigation-menu namespace.
    /// </summary>
    /// <param name="streamKey">The candidate stream key.</param>
    /// <returns><see langword="true"/> when the prefix matches, without validating the identifier suffix.</returns>
public static bool IsMenuStream(string? streamKey)
        => streamKey?.StartsWith("nav-menu-", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Determines whether a stream key uses the site-navigation-settings namespace.
    /// </summary>
    /// <param name="streamKey">The candidate stream key.</param>
    /// <returns><see langword="true"/> when the prefix matches, without validating the identifier suffix.</returns>
public static bool IsSiteSettingsStream(string? streamKey)
        => streamKey?.StartsWith("site-nav-settings-", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Extracts the numeric identifier from a navigation-menu stream key.
    /// </summary>
    /// <param name="streamKey">A key produced by <see cref="Menu"/>.</param>
    /// <returns>The parsed identifier.</returns>
    /// <exception cref="InvalidOperationException">The key has the wrong prefix or a non-numeric suffix.</exception>
public static long ExtractMenuId(string streamKey)
        => ExtractLongId(streamKey, "nav-menu-");

    /// <summary>
    /// Extracts the numeric identifier from a site-navigation-settings stream key.
    /// </summary>
    /// <param name="streamKey">A key produced by <see cref="SiteSettings"/>.</param>
    /// <returns>The parsed site identifier.</returns>
    /// <exception cref="InvalidOperationException">The key has the wrong prefix or a non-numeric suffix.</exception>
public static long ExtractSiteId(string streamKey)
        => ExtractLongId(streamKey, "site-nav-settings-");

    /// <summary>
    /// Parses a long suffix after an expected case-insensitive stream-key prefix.
    /// </summary>
    /// <param name="streamKey">The stream key to parse.</param>
    /// <param name="prefix">The required namespace prefix.</param>
    /// <returns>The parsed suffix.</returns>
    /// <exception cref="InvalidOperationException">The key does not match the required format.</exception>
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
