using Aero.Cms.Modules.Footer.Domain;

namespace Aero.Cms.Modules.Footer.Events;

public sealed record FooterCreated(
    long SiteId,
    string Name,
    string Key,
    string? Description,
    long? UserId,
    DateTimeOffset CreatedOn,
    string Culture = "en-US",
    long? TranslationGroupId = null);

public sealed record FooterDraftSaved(
    long SiteId,
    string Name,
    string Key,
    string? Description,
    FooterSnapshot Snapshot,
    long? UserId,
    DateTimeOffset SavedOn,
    string? ChangeNote);

public sealed record FooterPublished(
    long SiteId,
    FooterSnapshot Snapshot,
    long? UserId,
    DateTimeOffset PublishedOn,
    string? ChangeNote);

public sealed record FooterArchived(
    long SiteId,
    long? UserId,
    DateTimeOffset ArchivedOn);

public sealed record SiteDefaultFooterChanged(
    long SiteId,
    long? FooterId,
    long? UserId,
    DateTimeOffset ChangedOn);

public static class FooterStreams
{
    public static string Footer(long id) => $"footer-{id}";
    public static string SiteSettings(long siteId) => $"site-footer-settings-{siteId}";

    public static bool IsFooterStream(string? streamKey)
        => streamKey?.StartsWith("footer-", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsSiteSettingsStream(string? streamKey)
        => streamKey?.StartsWith("site-footer-settings-", StringComparison.OrdinalIgnoreCase) == true;

    public static long ExtractFooterId(string streamKey)
        => ExtractLongId(streamKey, "footer-");

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
