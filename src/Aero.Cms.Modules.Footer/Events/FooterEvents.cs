using Aero.Cms.Modules.Footer.Domain;

namespace Aero.Cms.Modules.Footer.Events;

/// <summary>
/// Represents a record for FooterCreated.
/// </summary>
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
/// Represents a record for FooterDraftSaved.
/// </summary>
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
/// Represents a record for FooterPublished.
/// </summary>
public sealed record FooterPublished(
    long SiteId,
    FooterSnapshot Snapshot,
    long? UserId,
    DateTimeOffset PublishedOn,
    string? ChangeNote);

/// <summary>
/// Represents a record for FooterArchived.
/// </summary>
public sealed record FooterArchived(
    long SiteId,
    long? UserId,
    DateTimeOffset ArchivedOn);

/// <summary>
/// Represents a record for SiteDefaultFooterChanged.
/// </summary>
public sealed record SiteDefaultFooterChanged(
    long SiteId,
    long? FooterId,
    long? UserId,
    DateTimeOffset ChangedOn);

/// <summary>
/// Represents a class for FooterStreams.
/// </summary>
public static class FooterStreams
{
        /// <summary>
    /// Footer method.
    /// </summary>
public static string Footer(long id) => $"footer-{id}";
        /// <summary>
    /// SiteSettings method.
    /// </summary>
public static string SiteSettings(long siteId) => $"site-footer-settings-{siteId}";

        /// <summary>
    /// IsFooterStream method.
    /// </summary>
public static bool IsFooterStream(string? streamKey)
        => streamKey?.StartsWith("footer-", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
    /// IsSiteSettingsStream method.
    /// </summary>
public static bool IsSiteSettingsStream(string? streamKey)
        => streamKey?.StartsWith("site-footer-settings-", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
    /// ExtractFooterId method.
    /// </summary>
public static long ExtractFooterId(string streamKey)
        => ExtractLongId(streamKey, "footer-");

        /// <summary>
    /// ExtractSiteId method.
    /// </summary>
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
