using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Events;

public sealed record NavMenuCreated(
    long SiteId,
    string Name,
    string Key,
    long? UserId,
    DateTimeOffset CreatedOn);

public sealed record NavMenuDraftSaved(
    long SiteId,
    string Name,
    string Key,
    NavMenuSnapshot Snapshot,
    long? UserId,
    DateTimeOffset SavedOn,
    string? ChangeNote);

public sealed record NavMenuPublished(
    long SiteId,
    NavMenuSnapshot Snapshot,
    long? UserId,
    DateTimeOffset PublishedOn,
    string? ChangeNote);

public sealed record NavMenuArchived(
    long SiteId,
    long? UserId,
    DateTimeOffset ArchivedOn);

public sealed record SiteDefaultNavMenuChanged(
    long SiteId,
    long? NavMenuId,
    long? UserId,
    DateTimeOffset ChangedOn);

public static class NavMenuStreams
{
    public static string Menu(long id) => $"nav-menu-{id}";
    public static string SiteSettings(long siteId) => $"site-nav-settings-{siteId}";

    public static bool IsMenuStream(string? streamKey)
        => streamKey?.StartsWith("nav-menu-", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsSiteSettingsStream(string? streamKey)
        => streamKey?.StartsWith("site-nav-settings-", StringComparison.OrdinalIgnoreCase) == true;

    public static long ExtractMenuId(string streamKey)
        => ExtractLongId(streamKey, "nav-menu-");

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
