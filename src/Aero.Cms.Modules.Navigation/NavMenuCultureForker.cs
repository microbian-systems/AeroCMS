using System.Globalization;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;

namespace Aero.Cms.Modules.Navigation;

public sealed record NavMenuCultureFork(
    NavMenuCreated Created,
    NavMenuDraftSaved DraftSaved);

public static class NavMenuCultureForker
{
    public static NavMenuCultureFork Fork(
        NavMenuDocument source,
        NavMenuSnapshot sourceSnapshot,
        long targetMenuId,
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

        var created = new NavMenuCreated(
            source.SiteId,
            source.Name,
            source.Key,
            userId,
            forkedOn,
            culture,
            translationSetId);

        var draftSaved = new NavMenuDraftSaved(
            source.SiteId,
            source.Name,
            source.Key,
            snapshot,
            userId,
            forkedOn,
            $"Forked from {source.Culture} to {culture}");

        return new NavMenuCultureFork(created, draftSaved);
    }

    private static NavMenuSnapshot CloneSnapshot(NavMenuSnapshot snapshot)
        => new()
        {
            Layout = snapshot.Layout,
            Responsive = snapshot.Responsive,
            Style = snapshot.Style,
            SiteLogoUrl = snapshot.SiteLogoUrl,
            Left = snapshot.Left.Select(CloneComponent).ToList(),
            Center = snapshot.Center.Select(CloneComponent).ToList(),
            Right = snapshot.Right.Select(CloneComponent).ToList()
        };

    private static INavMenuComponent CloneComponent(INavMenuComponent component)
        => component switch
        {
            NavLink link => link with { },
            NavMenu menu => menu with { Children = menu.Children.Select(CloneComponent).ToList() },
            NavHtml html => html with { },
            NavSearch search => search with { },
            _ => component
        };

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
