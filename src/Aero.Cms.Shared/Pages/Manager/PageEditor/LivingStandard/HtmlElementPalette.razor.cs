using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class HtmlElementPalette
{
    private static readonly IReadOnlyList<LayoutOption> LayoutOptions =
    [
        new(HtmlLayoutStarterKind.OneColumn, "1 column", "One full-width content area", "▭"),
        new(HtmlLayoutStarterKind.TwoColumns, "2 columns", "Two equal content areas", "▯▯"),
        new(HtmlLayoutStarterKind.ThreeColumns, "3 columns", "Three equal content areas", "▯▯▯"),
        new(HtmlLayoutStarterKind.Split, "Split", "Two flexible side-by-side areas", "◧"),
        new(HtmlLayoutStarterKind.CardGrid, "Cards", "A responsive three-card grid", "▦")
    ];

    private IReadOnlyList<ElementGroup> _groups = [];
    private string _searchText = string.Empty;

    [Parameter, EditorRequired]
    public IReadOnlyList<HtmlElementDefinition> Elements { get; set; } = [];

    [Parameter]
    public EventCallback<string> ElementRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlLayoutStarterKind> LayoutRequested { get; set; }

    private string SearchText
    {
        get => _searchText;
        set => _searchText = value ?? string.Empty;
    }

    private IReadOnlyList<ElementGroup> FilteredGroups => _groups
        .Select(group => new ElementGroup(
            group.Category,
            group.Elements
                .Where(MatchesSearch)
                .ToArray()))
        .Where(group => group.Elements.Count > 0)
        .ToArray();

    protected override void OnParametersSet()
    {
        _groups = Elements
            .Where(element => !element.Tag.Equals("li", StringComparison.OrdinalIgnoreCase))
            .GroupBy(element => element.PaletteCategory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ElementGroup(
                group.Key,
                group.OrderBy(element => element.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();
    }

    private bool MatchesSearch(HtmlElementDefinition element) =>
        string.IsNullOrWhiteSpace(SearchText)
        || element.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        || element.Tag.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    private Task RequestElementAsync(string tagName) => ElementRequested.InvokeAsync(tagName);

    private Task RequestLayoutAsync(HtmlLayoutStarterKind kind) => LayoutRequested.InvokeAsync(kind);

    private sealed record ElementGroup(string Category, IReadOnlyList<HtmlElementDefinition> Elements);

    private sealed record LayoutOption(
        HtmlLayoutStarterKind Kind,
        string Label,
        string Description,
        string Icon);
}
