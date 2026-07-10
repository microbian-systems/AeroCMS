using System.Globalization;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;

namespace Aero.Cms.Modules.Navigation;

/// <summary>
/// Represents a record for NavMenuCultureFork.
/// </summary>
public sealed record NavMenuCultureFork(
    NavMenuCreated Created,
    NavMenuDraftSaved DraftSaved);

/// <summary>
/// Represents a class for NavMenuCultureForker.
/// </summary>
public static class NavMenuCultureForker
{
        /// <summary>
    /// Fork method.
    /// </summary>
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
        var TranslationGroupId = source.TranslationGroupId ?? source.Id;
        var culture = NormalizeCulture(targetCulture);
        var snapshot = CloneSnapshot(sourceSnapshot);

        var created = new NavMenuCreated(
            source.SiteId,
            source.Name,
            source.Key,
            userId,
            forkedOn,
            culture,
            TranslationGroupId);

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
            Rows = snapshot.Rows.Select(CloneRow).ToList(),
            Left = snapshot.Left.Select(CloneComponent).ToList(),
            Center = snapshot.Center.Select(CloneComponent).ToList(),
            Right = snapshot.Right.Select(CloneComponent).ToList()
        };

    private static NavCanvasRow CloneRow(NavCanvasRow row)
        => row with
        {
            Columns = row.Columns.Select(CloneColumn).ToList()
        };

    private static NavCanvasColumn CloneColumn(NavCanvasColumn column)
        => column with
        {
            Blocks = column.Blocks.Select(CloneBlock).ToList()
        };

    private static NavCanvasBlock CloneBlock(NavCanvasBlock block)
        => block with
        {
            Component = CloneComponent(block.Component)
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
