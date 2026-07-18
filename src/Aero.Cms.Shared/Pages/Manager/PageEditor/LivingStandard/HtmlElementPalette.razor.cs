using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class HtmlElementPalette
{
    private const int InitialComponentCount = 6;

    private static readonly HashSet<string> BasicElementTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "section", "div", "h1", "h2", "h3", "p", "a", "button", "img", "figure",
        "ul", "ol", "hr", "blockquote", "details", "table", "form", "audio", "video"
    };

    private static readonly IReadOnlyList<ComponentOption> ComponentOptions =
    [
        new(HtmlComponentTemplateKind.Hero, "Hero", "A centered introduction with primary actions", "◆", "Start here"),
        new(HtmlComponentTemplateKind.SplitHero, "Hero + image", "A responsive split hero with editable image and actions", "◩", "Start here"),
        new(HtmlComponentTemplateKind.FeatureGrid, "Features", "A responsive three-card feature section", "▦", "Content"),
        new(HtmlComponentTemplateKind.FeatureList, "Feature list", "A responsive numbered benefit list with supporting copy", "☷", "Content"),
        new(HtmlComponentTemplateKind.CallToAction, "Call to action", "A focused prompt with one primary action", "→", "Conversion"),
        new(HtmlComponentTemplateKind.CenteredCallToAction, "CTA + image", "A centered call to action over an editable background image", "◎", "Conversion"),
        new(HtmlComponentTemplateKind.FrequentlyAskedQuestions, "FAQ", "A responsive question-and-answer section", "?", "Trust"),
        new(HtmlComponentTemplateKind.AccordionFaq, "FAQ accordion", "Expandable semantic questions and answers", "⌄", "Trust"),
        new(HtmlComponentTemplateKind.Testimonial, "Testimonial", "A highlighted customer quotation", "“”", "Trust"),
        new(HtmlComponentTemplateKind.Statistics, "Statistics", "Three responsive headline metrics", "%", "Trust"),
        new(HtmlComponentTemplateKind.ImageAndText, "Image + text", "A responsive visual and copy split", "◫", "Content"),
        new(HtmlComponentTemplateKind.ContactForm, "Contact form", "A static, accessible contact section", "✉", "Conversion"),
        new(HtmlComponentTemplateKind.Gallery, "Gallery", "A responsive three-image gallery", "▧", "Content"),
        new(HtmlComponentTemplateKind.NavigationHeader, "Navigation", "A responsive site header with editable links", "☰", "Navigation"),
        new(HtmlComponentTemplateKind.LogoCloud, "Partner logos", "An accessible grid of editable partner names", "✦", "Trust"),
        new(HtmlComponentTemplateKind.PricingGrid, "Pricing", "Three responsive plans with benefits and actions", "¤", "Conversion"),
        new(HtmlComponentTemplateKind.TeamGrid, "Team", "A responsive team section with editable portraits", "♙", "Trust"),
        new(HtmlComponentTemplateKind.SiteFooter, "Footer links", "A responsive page section of editable link groups", "▤", "Navigation"),
        new(HtmlComponentTemplateKind.NewsletterSignup, "Newsletter", "A static email signup section ready for later processing", "✉", "Conversion"),
        new(HtmlComponentTemplateKind.AnnouncementBanner, "Announcement", "A responsive update banner with one ordinary link", "!", "Navigation"),
        new(HtmlComponentTemplateKind.LatestArticles, "Latest articles", "Three responsive static article cards", "▥", "Content"),
        new(HtmlComponentTemplateKind.ProcessSteps, "Process steps", "Three numbered steps with editable explanations", "①", "Structure"),
        new(HtmlComponentTemplateKind.ShowcaseCollection, "Collection", "A responsive three-item static collection", "▦", "Content"),
        new(HtmlComponentTemplateKind.MilestoneTimeline, "Timeline", "Three dated milestones in a semantic ordered timeline", "◷", "Structure"),
        new(HtmlComponentTemplateKind.FeatureComparisonTable, "Comparison table", "A compact editable feature comparison", "▤", "Structure"),
        new(HtmlComponentTemplateKind.DetailsList, "Details list", "Responsive editable terms and descriptions", "☷", "Structure"),
        new(HtmlComponentTemplateKind.ConfirmationDialog, "Confirmation dialog", "An editable open dialog with two static actions", "▣", "Conversion"),
    ];

    private static readonly IReadOnlyList<LayoutOption> LayoutOptions =
    [
        new(HtmlLayoutStarterKind.OneColumn, "1 column", "One full-width content area", "▭"),
        new(HtmlLayoutStarterKind.TwoColumns, "2 columns", "Two equal content areas", "▯▯"),
        new(HtmlLayoutStarterKind.ThreeColumns, "3 columns", "Three equal content areas", "▯▯▯"),
        new(HtmlLayoutStarterKind.FourColumns, "4 columns", "Four equal content areas that stack on small screens", "▥"),
        new(HtmlLayoutStarterKind.Split, "Split", "Two flexible side-by-side areas", "◧"),
        new(HtmlLayoutStarterKind.HeadingTwoColumns, "Heading + 2", "A heading area followed by two responsive columns", "▤"),
        new(HtmlLayoutStarterKind.CardGrid, "Cards", "A responsive three-card grid", "▦")
    ];

    private IReadOnlyList<ElementGroup> _groups = [];
    private string _searchText = string.Empty;
    private bool _showAdvanced;
    private bool _showAllComponents;

    [Parameter, EditorRequired]
    public IReadOnlyList<HtmlElementDefinition> Elements { get; set; } = [];

    [Parameter]
    public EventCallback<string> ElementRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlLayoutStarterKind> LayoutRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlComponentTemplateKind> ComponentRequested { get; set; }

    [Parameter]
    public EventCallback ImportRequested { get; set; }

    private string SearchText
    {
        get => _searchText;
        set => _searchText = value ?? string.Empty;
    }

    private bool ShowingAllElements => _showAdvanced || !string.IsNullOrWhiteSpace(SearchText);

    private bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);

    private bool CanToggleComponents => string.IsNullOrWhiteSpace(SearchText)
        && ComponentOptions.Count > InitialComponentCount;

    private IReadOnlyList<ComponentOption> FilteredComponentOptions => ComponentOptions
        .Where(option => MatchesSearch(option.Label, option.Description))
        .Take(CanToggleComponents && !_showAllComponents ? InitialComponentCount : ComponentOptions.Count)
        .ToArray();

    private IReadOnlyList<ComponentGroup> FilteredComponentGroups => FilteredComponentOptions
        .GroupBy(option => option.Category, StringComparer.Ordinal)
        .Select(group => new ComponentGroup(group.Key, group.ToArray()))
        .ToArray();

    private IReadOnlyList<LayoutOption> FilteredLayoutOptions => LayoutOptions
        .Where(option => MatchesSearch(option.Label, option.Description))
        .ToArray();

    private IReadOnlyList<ElementGroup> FilteredGroups => _groups
        .Select(group => new ElementGroup(
            group.Category,
            group.Elements
                .Where(element => ShowingAllElements || BasicElementTags.Contains(element.Tag))
                .Where(MatchesSearch)
                .ToArray()))
        .Where(group => group.Elements.Count > 0)
        .ToArray();

    private bool HasNoMatches => !string.IsNullOrWhiteSpace(SearchText)
        && FilteredComponentOptions.Count == 0
        && FilteredLayoutOptions.Count == 0
        && FilteredGroups.Count == 0;

    private int VisibleItemCount => FilteredComponentOptions.Count
        + FilteredLayoutOptions.Count
        + FilteredGroups.Sum(group => group.Elements.Count);

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

    private bool MatchesSearch(string label, string description) =>
        string.IsNullOrWhiteSpace(SearchText)
        || label.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        || description.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    private Task RequestElementAsync(string tagName) => ElementRequested.InvokeAsync(tagName);

    private Task RequestLayoutAsync(HtmlLayoutStarterKind kind) => LayoutRequested.InvokeAsync(kind);

    private Task RequestComponentAsync(HtmlComponentTemplateKind kind) => ComponentRequested.InvokeAsync(kind);

    private Task RequestImportAsync() => ImportRequested.InvokeAsync();

    private void ToggleAdvanced() => _showAdvanced = !_showAdvanced;

    private void ToggleComponents() => _showAllComponents = !_showAllComponents;

    private static string ComponentGroupKey(string category) => $"component:{category}";

    private static string ElementGroupKey(string category) => $"element:{category}";

    private sealed record ElementGroup(string Category, IReadOnlyList<HtmlElementDefinition> Elements);

    private sealed record ComponentGroup(string Category, IReadOnlyList<ComponentOption> Options);

    private sealed record LayoutOption(
        HtmlLayoutStarterKind Kind,
        string Label,
        string Description,
        string Icon);

    private sealed record ComponentOption(
        HtmlComponentTemplateKind Kind,
        string Label,
        string Description,
        string Icon,
        string Category);
}
