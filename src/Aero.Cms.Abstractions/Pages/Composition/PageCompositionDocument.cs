namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Stores optional typed-content meaning beside a page's ordinary HTML tree.
/// </summary>
/// <remarks>
/// Every entry targets a stable HTML node identifier. The document contains no
/// rendered content and does not replace <c>HtmlPageContent</c>.
/// </remarks>
public sealed record PageCompositionDocument
{
    /// <summary>Gets the pageable content-list scopes in the page.</summary>
    public IReadOnlyList<PageContentListScope> ContentLists { get; init; } = [];

    /// <summary>Gets the single-item content scopes in the page.</summary>
    public IReadOnlyList<PageContentItemScope> ContentItems { get; init; } = [];

    /// <summary>Gets the field-to-HTML bindings in the page.</summary>
    public IReadOnlyList<PageFieldBinding> FieldBindings { get; init; } = [];

    /// <summary>Gets source-backed fragments rendered into ordinary HTML containers.</summary>
    public IReadOnlyList<PageRenderedFragment> RenderedFragments { get; init; } = [];

    /// <summary>Gets application fragments resolved only through an explicit provider registry.</summary>
    public IReadOnlyList<PageRegisteredFragment> RegisteredFragments { get; init; } = [];

    /// <summary>
    /// Creates an independent snapshot suitable for draft replacement or publication.
    /// </summary>
    /// <returns>A new composition document with independent collections.</returns>
    public PageCompositionDocument CreateSnapshot() => new()
    {
        ContentLists = (ContentLists ?? []).Select(scope => scope.CreateSnapshot()).ToArray(),
        ContentItems = (ContentItems ?? []).ToArray(),
        FieldBindings = (FieldBindings ?? []).ToArray(),
        RenderedFragments = (RenderedFragments ?? []).ToArray(),
        RegisteredFragments = (RegisteredFragments ?? []).Select(fragment => fragment.CreateSnapshot()).ToArray()
    };
}
