namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Identifies the renderer used to expand a source-backed page fragment.
/// </summary>
public enum PageRenderedFragmentKind
{
    /// <summary>Renders Markdown source through the Pages Markdown strategy.</summary>
    Markdown = 0,

    /// <summary>Imports author-supplied HTML through the Pages HTML policy.</summary>
    CustomHtml = 1,

    /// <summary>Renders a bounded Scriban template with an allowlisted context.</summary>
    Scriban = 2,

    /// <summary>Renders SharpTS through Aero's bounded interpreted TypeScript host.</summary>
    SharpTs = 3,

    /// <summary>Imports HTML enhanced with Aero's bounded HTMX attribute policy.</summary>
    Htmx = 4
}

/// <summary>
/// Stores source-backed rendering intent beside an ordinary HTML container.
/// </summary>
/// <remarks>
/// The targeted node remains part of <c>HtmlPageContent</c>. Rendering replaces
/// only that node's children in an ephemeral page snapshot.
/// </remarks>
public sealed record PageRenderedFragment
{
    /// <summary>Maximum source length accepted for one fragment.</summary>
    public const int MaximumSourceLength = 50_000;

    /// <summary>Maximum number of source-backed fragments accepted on one page.</summary>
    public const int MaximumFragmentsPerPage = 100;

    /// <summary>Gets the stable HTML element that owns the rendered output.</summary>
    public long NodeId { get; init; }

    /// <summary>Gets the rendering strategy for <see cref="Source"/>.</summary>
    public PageRenderedFragmentKind Kind { get; init; }

    /// <summary>Gets the authoring source retained in the composition sidecar.</summary>
    public string Source { get; init; } = string.Empty;
}
