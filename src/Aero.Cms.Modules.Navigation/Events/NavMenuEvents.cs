using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Events;

/// <summary>
/// Represents a record for NavMenuCreated.
/// </summary>
public sealed record NavMenuCreated(
    long SiteId,
    string Name,
    string Key,
    long? UserId,
    DateTimeOffset CreatedOn,
    string Culture = "en-US",
    long? TranslationGroupId = null);

/// <summary>
/// Represents a record for NavMenuDraftSaved.
/// </summary>
public sealed record NavMenuDraftSaved(
    long SiteId,
    string Name,
    string Key,
    NavMenuSnapshot Snapshot,
    long? UserId,
    DateTimeOffset SavedOn,
    string? ChangeNote);

/// <summary>
/// Represents a record for NavMenuPublished.
/// </summary>
public sealed record NavMenuPublished(
    long SiteId,
    NavMenuSnapshot Snapshot,
    long? UserId,
    DateTimeOffset PublishedOn,
    string? ChangeNote);

/// <summary>
/// Represents a record for NavMenuArchived.
/// </summary>
public sealed record NavMenuArchived(
    long SiteId,
    long? UserId,
    DateTimeOffset ArchivedOn);

/// <summary>
/// Represents a record for SiteDefaultNavMenuChanged.
/// </summary>
public sealed record SiteDefaultNavMenuChanged(
    long SiteId,
    long? NavMenuId,
    long? UserId,
    DateTimeOffset ChangedOn);

/// <summary>
/// Represents a class for NavMenuStreams.
/// </summary>
public static class NavMenuStreams
{
        /// <summary>
    /// Menu method.
    /// </summary>
public static string Menu(long id) => $"nav-menu-{id}";
        /// <summary>
    /// SiteSettings method.
    /// </summary>
public static string SiteSettings(long siteId) => $"site-nav-settings-{siteId}";

        /// <summary>
    /// IsMenuStream method.
    /// </summary>
public static bool IsMenuStream(string? streamKey)
        => streamKey?.StartsWith("nav-menu-", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
    /// IsSiteSettingsStream method.
    /// </summary>
public static bool IsSiteSettingsStream(string? streamKey)
        => streamKey?.StartsWith("site-nav-settings-", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
    /// ExtractMenuId method.
    /// </summary>
public static long ExtractMenuId(string streamKey)
        => ExtractLongId(streamKey, "nav-menu-");

        /// <summary>
    /// ExtractSiteId method.
    /// </summary>
public static long ExtractSiteId(string streamKey)
        => ExtractLongId(streamKey, "site-nav-settings-");

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
