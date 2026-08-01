using System.Globalization;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;

namespace Aero.Cms.Modules.Navigation;

/// <summary>
/// Contains the creation and initial-draft events required to start a translated navigation stream.
/// </summary>
/// <param name="Created">The event that establishes the new culture variant.</param>
/// <param name="DraftSaved">The event carrying the cloned editable snapshot.</param>
public sealed record NavMenuCultureFork(
    NavMenuCreated Created,
    NavMenuDraftSaved DraftSaved);

/// <summary>
/// Creates culture-fork events without mutating the source document or component collections.
/// </summary>
public static class NavMenuCultureForker
{
    /// <summary>
    /// Builds the two initial events for a culture variant of an existing navigation menu.
    /// </summary>
    /// <param name="source">The source menu document.</param>
    /// <param name="sourceSnapshot">The source's current editor snapshot.</param>
    /// <param name="targetMenuId">
    /// The identifier reserved by the caller for the new stream. It is not embedded in either
    /// event because the caller supplies it when starting the stream.
    /// </param>
    /// <param name="targetCulture">The requested culture; missing or invalid names become <c>en-US</c>.</param>
    /// <param name="userId">The optional actor recorded on both events.</param>
    /// <param name="timestamp">The shared event timestamp, or UTC now when omitted.</param>
    /// <returns>Creation and initial-draft events with independent row, column, block, and component lists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="sourceSnapshot"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Copies a snapshot and its mutable component collections for independent editing.
    /// </summary>
    /// <param name="snapshot">The source snapshot.</param>
    /// <returns>The copied snapshot.</returns>
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

    /// <summary>
    /// Copies a row and its column list.
    /// </summary>
    /// <param name="row">The source row.</param>
    /// <returns>The copied row.</returns>
    private static NavCanvasRow CloneRow(NavCanvasRow row)
        => row with
        {
            Columns = row.Columns.Select(CloneColumn).ToList()
        };

    /// <summary>
    /// Copies a column and its block list.
    /// </summary>
    /// <param name="column">The source column.</param>
    /// <returns>The copied column.</returns>
    private static NavCanvasColumn CloneColumn(NavCanvasColumn column)
        => column with
        {
            Blocks = column.Blocks.Select(CloneBlock).ToList()
        };

    /// <summary>
    /// Copies a block and clones its component where the component type has mutable children.
    /// </summary>
    /// <param name="block">The source block.</param>
    /// <returns>The copied block.</returns>
    private static NavCanvasBlock CloneBlock(NavCanvasBlock block)
        => block with
        {
            Component = CloneComponent(block.Component)
        };

    /// <summary>
    /// Copies known component records and recursively duplicates menu child collections.
    /// </summary>
    /// <param name="component">The component to clone.</param>
    /// <returns>
    /// A copied known component; unhandled immutable component implementations are returned unchanged.
    /// </returns>
    private static INavMenuComponent CloneComponent(INavMenuComponent component)
        => component switch
        {
            NavLink link => link with { },
            NavMenu menu => menu with { Children = menu.Children.Select(CloneComponent).ToList() },
            NavHtml html => html with { },
            NavSearch search => search with { },
            _ => component
        };

    /// <summary>
    /// Canonicalizes a culture name for the fork event.
    /// </summary>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The canonical name, or <c>en-US</c> when the input is blank or invalid.</returns>
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
