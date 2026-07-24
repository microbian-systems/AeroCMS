using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Presents searchable HTML elements, responsive layout starters, and static component
/// templates that can be inserted into the page editor.
/// </summary>
/// <remarks>
/// The palette emits insertion intent only. The owning editor remains responsible for choosing
/// a valid insertion point and enforcing the content-model policy.
/// </remarks>
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

    private static readonly IReadOnlyList<RenderedFragmentOption> RenderedFragmentOptions =
    [
        new(
            PageRenderedFragmentKind.Markdown,
            "Markdown",
            "A Markdown block rendered safely through Markdig",
            "M↓"),
        new(
            PageRenderedFragmentKind.CustomHtml,
            "Custom HTML",
            "A validated HTML fragment restricted to supported elements and safe attributes",
            "</>"),
        new(
            PageRenderedFragmentKind.Scriban,
            "Scriban",
            "A bounded server-rendered template with explicit page and site context",
            "{{ }}"),
        new(
            PageRenderedFragmentKind.SharpTs,
            "TS",
            "A server-rendered TypeScript fragment using Aero's html tagged template",
            "TS"),
        new(
            PageRenderedFragmentKind.Htmx,
            "HTMX",
            "Validated HTML with same-origin HTMX interactions",
            "hx")
    ];

    private IReadOnlyList<ElementGroup> _groups = [];
    private string _searchText = string.Empty;
    private bool _showAdvanced;
    private bool _showAllComponents;

    /// <summary>
    /// Gets or sets the element catalog entries available for palette grouping and filtering.
    /// </summary>
    /// <remarks>
    /// Entries not marked as palette-visible are omitted. List items are also omitted because
    /// they are created through guided list actions.
    /// </remarks>
    [Parameter, EditorRequired]
    public IReadOnlyList<HtmlElementDefinition> Elements { get; set; } = [];

    /// <summary>
    /// Gets or sets the callback invoked with the tag name of an element insertion request.
    /// </summary>
    [Parameter]
    public EventCallback<string> ElementRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked with the selected responsive layout starter.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlLayoutStarterKind> LayoutRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked with the selected static component template.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlComponentTemplateKind> ComponentRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked with the selected source-rendered fragment strategy.
    /// </summary>
    [Parameter]
    public EventCallback<PageRenderedFragmentKind> RenderedFragmentRequested { get; set; }

    /// <summary>Gets or sets whether an enabled, configured AI provider is available.</summary>
    [Parameter]
    public bool AiEnabled { get; set; }

    /// <summary>Gets or sets the explanatory disabled-state tooltip for AI actions.</summary>
    [Parameter]
    public string AiUnavailableMessage { get; set; }
        = "Configure and enable an AI provider to generate this fragment.";

    /// <summary>Gets or sets the source-fragment AI action callback.</summary>
    [Parameter]
    public EventCallback<PageRenderedFragmentKind> AiRequested { get; set; }

    /// <summary>Gets the server-supplied registered application-fragment catalog.</summary>
    [Parameter]
    public IReadOnlyList<PageRegisteredFragmentDescriptor> RegisteredFragments { get; set; } = [];

    /// <summary>Gets or sets the callback for a registered application-fragment key.</summary>
    [Parameter]
    public EventCallback<string> RegisteredFragmentRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests the HTML-fragment import workflow.
    /// </summary>
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

    private IReadOnlyList<RenderedFragmentOption> FilteredRenderedFragmentOptions => RenderedFragmentOptions
        .Where(option => MatchesSearch(option.Label, option.Description))
        .ToArray();

    private IReadOnlyList<PageRegisteredFragmentDescriptor> FilteredRegisteredFragments => RegisteredFragments
        .Where(descriptor => MatchesSearch(descriptor.DisplayName, descriptor.Description ?? descriptor.Key))
        .OrderBy(descriptor => descriptor.Category, StringComparer.OrdinalIgnoreCase)
        .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
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
        && FilteredRenderedFragmentOptions.Count == 0
        && FilteredRegisteredFragments.Count == 0
        && FilteredLayoutOptions.Count == 0
        && FilteredGroups.Count == 0;

    private int VisibleItemCount => FilteredComponentOptions.Count
        + FilteredRenderedFragmentOptions.Count
        + FilteredRegisteredFragments.Count
        + FilteredLayoutOptions.Count
        + FilteredGroups.Sum(group => group.Elements.Count);

    /// <summary>
    /// Rebuilds the categorized element groups from the current catalog parameter.
    /// </summary>
    /// <remarks>
    /// Category and element ordering use ordinal, case-insensitive comparison so the rendered
    /// palette remains deterministic across cultures.
    /// </remarks>
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

    /// <summary>
    /// Determines whether an element's display name or tag matches the current search text.
    /// </summary>
    /// <param name="element">The catalog definition to test.</param>
    /// <returns><see langword="true"/> when the element should remain visible.</returns>
    private bool MatchesSearch(HtmlElementDefinition element) =>
        string.IsNullOrWhiteSpace(SearchText)
        || element.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        || element.Tag.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether a palette option's label or description matches the current search.
    /// </summary>
    /// <param name="label">The user-facing option label.</param>
    /// <param name="description">The user-facing option description.</param>
    /// <returns><see langword="true"/> when the option should remain visible.</returns>
    private bool MatchesSearch(string label, string description) =>
        string.IsNullOrWhiteSpace(SearchText)
        || label.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        || description.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Forwards an element insertion request to the owning editor.
    /// </summary>
    /// <param name="tagName">The catalog tag to insert.</param>
    /// <returns>A task that completes when the callback has finished.</returns>
    private Task RequestElementAsync(string tagName) => ElementRequested.InvokeAsync(tagName);

    /// <summary>
    /// Forwards a layout starter insertion request to the owning editor.
    /// </summary>
    /// <param name="kind">The selected layout starter.</param>
    /// <returns>A task that completes when the callback has finished.</returns>
    private Task RequestLayoutAsync(HtmlLayoutStarterKind kind) => LayoutRequested.InvokeAsync(kind);

    /// <summary>
    /// Forwards a static component insertion request to the owning editor.
    /// </summary>
    /// <param name="kind">The selected component template.</param>
    /// <returns>A task that completes when the callback has finished.</returns>
    private Task RequestComponentAsync(HtmlComponentTemplateKind kind) => ComponentRequested.InvokeAsync(kind);

    /// <summary>Forwards a source-rendered fragment request to the owning editor.</summary>
    private Task RequestRenderedFragmentAsync(PageRenderedFragmentKind kind) =>
        RenderedFragmentRequested.InvokeAsync(kind);

    private Task RequestAiAsync(PageRenderedFragmentKind kind) =>
        AiEnabled ? AiRequested.InvokeAsync(kind) : Task.CompletedTask;

    private static bool IsAiEligible(PageRenderedFragmentKind kind) =>
        kind is PageRenderedFragmentKind.Markdown
            or PageRenderedFragmentKind.CustomHtml
            or PageRenderedFragmentKind.Scriban
            or PageRenderedFragmentKind.SharpTs
            or PageRenderedFragmentKind.Htmx;

    private Task RequestRegisteredFragmentAsync(string key) =>
        RegisteredFragmentRequested.InvokeAsync(key);

    /// <summary>
    /// Requests the fragment import workflow from the owning editor.
    /// </summary>
    /// <returns>A task that completes when the callback has finished.</returns>
    private Task RequestImportAsync() => ImportRequested.InvokeAsync();

    /// <summary>
    /// Toggles visibility of catalog elements outside the basic element allowlist.
    /// </summary>
    private void ToggleAdvanced() => _showAdvanced = !_showAdvanced;

    /// <summary>
    /// Toggles between the initial component subset and the complete component catalog.
    /// </summary>
    private void ToggleComponents() => _showAllComponents = !_showAllComponents;

    /// <summary>
    /// Creates a stable render key for a component category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>A key namespaced to component groups.</returns>
    private static string ComponentGroupKey(string category) => $"component:{category}";

    /// <summary>
    /// Creates a stable render key for an element category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>A key namespaced to element groups.</returns>
    private static string ElementGroupKey(string category) => $"element:{category}";

    /// <summary>
    /// Groups catalog elements under one palette category.
    /// </summary>
    /// <param name="Category">The display category.</param>
    /// <param name="Elements">The elements in deterministic display order.</param>
    private sealed record ElementGroup(string Category, IReadOnlyList<HtmlElementDefinition> Elements);

    /// <summary>
    /// Groups static component templates under one palette category.
    /// </summary>
    /// <param name="Category">The display category.</param>
    /// <param name="Options">The component choices in display order.</param>
    private sealed record ComponentGroup(string Category, IReadOnlyList<ComponentOption> Options);

    /// <summary>
    /// Describes one responsive layout starter presented by the palette.
    /// </summary>
    /// <param name="Kind">The layout starter identifier emitted to the owner.</param>
    /// <param name="Label">The compact display label.</param>
    /// <param name="Description">The user-facing layout description.</param>
    /// <param name="Icon">The text glyph displayed for the option.</param>
    private sealed record LayoutOption(
        HtmlLayoutStarterKind Kind,
        string Label,
        string Description,
        string Icon);

    /// <summary>
    /// Describes one static component template presented by the palette.
    /// </summary>
    /// <param name="Kind">The component template identifier emitted to the owner.</param>
    /// <param name="Label">The compact display label.</param>
    /// <param name="Description">The user-facing component description.</param>
    /// <param name="Icon">The text glyph displayed for the option.</param>
    /// <param name="Category">The palette category used for grouping.</param>
    private sealed record ComponentOption(
        HtmlComponentTemplateKind Kind,
        string Label,
        string Description,
        string Icon,
        string Category);

    /// <summary>Describes one source-backed fragment presented by the palette.</summary>
    private sealed record RenderedFragmentOption(
        PageRenderedFragmentKind Kind,
        string Label,
        string Description,
        string Icon);
}
