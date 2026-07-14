using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class HtmlElementPalette
{
    private static readonly HashSet<string> BasicElementTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "section", "div", "h1", "h2", "h3", "p", "a", "button", "img", "figure",
        "ul", "ol", "hr", "blockquote", "details", "table", "form", "audio", "video"
    };

    private static readonly IReadOnlyList<ComponentOption> ComponentOptions =
    [
        new(HtmlComponentTemplateKind.Hero, "Hero", "A centered introduction with primary actions", "◆"),
        new(HtmlComponentTemplateKind.FeatureGrid, "Features", "A responsive three-card feature section", "▦"),
        new(HtmlComponentTemplateKind.CallToAction, "Call to action", "A focused prompt with one primary action", "→"),
        new(HtmlComponentTemplateKind.FrequentlyAskedQuestions, "FAQ", "A responsive question-and-answer section", "?"),
        new(HtmlComponentTemplateKind.Testimonial, "Testimonial", "A highlighted customer quotation", "“”"),
        new(HtmlComponentTemplateKind.Statistics, "Statistics", "Three responsive headline metrics", "%"),
        new(HtmlComponentTemplateKind.ImageAndText, "Image + text", "A responsive visual and copy split", "◫"),
        new(HtmlComponentTemplateKind.ContactForm, "Contact form", "A static, accessible contact section", "✉"),
        new(HtmlComponentTemplateKind.Gallery, "Gallery", "A responsive three-image gallery", "▧"),
    ];

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
    private bool _showAdvanced;

    [Parameter, EditorRequired]
    public IReadOnlyList<HtmlElementDefinition> Elements { get; set; } = [];

    [Parameter]
    public EventCallback<string> ElementRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlLayoutStarterKind> LayoutRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlComponentTemplateKind> ComponentRequested { get; set; }

    private string SearchText
    {
        get => _searchText;
        set => _searchText = value ?? string.Empty;
    }

    private bool ShowingAllElements => _showAdvanced || !string.IsNullOrWhiteSpace(SearchText);

    private IReadOnlyList<ElementGroup> FilteredGroups => _groups
        .Select(group => new ElementGroup(
            group.Category,
            group.Elements
                .Where(element => ShowingAllElements || BasicElementTags.Contains(element.Tag))
                .Where(MatchesSearch)
                .ToArray()))
        .Where(group => group.Elements.Count > 0)
        .ToArray();

    protected override void OnParametersSet()
    {
        _groups = Elements
            .Where(element => element.IsPaletteVisible
                && !element.Tag.Equals("li", StringComparison.OrdinalIgnoreCase))
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

    private Task RequestComponentAsync(HtmlComponentTemplateKind kind) => ComponentRequested.InvokeAsync(kind);

    private void ToggleAdvanced() => _showAdvanced = !_showAdvanced;

    private sealed record ElementGroup(string Category, IReadOnlyList<HtmlElementDefinition> Elements);

    private sealed record LayoutOption(
        HtmlLayoutStarterKind Kind,
        string Label,
        string Description,
        string Icon);

    private sealed record ComponentOption(
        HtmlComponentTemplateKind Kind,
        string Label,
        string Description,
        string Icon);
}
