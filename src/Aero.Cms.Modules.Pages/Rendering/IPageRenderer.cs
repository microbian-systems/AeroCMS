using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Immutable site-owned page metadata exposed to renderer strategies.</summary>
/// <param name="Id">The persisted page identifier, or <see langword="null"/> for an unsaved preview.</param>
/// <param name="SiteId">The owning site identifier.</param>
/// <param name="RendererId">The stable selected renderer identifier.</param>
/// <param name="Title">The page title.</param>
/// <param name="Slug">The route slug.</param>
/// <param name="Path">The materialized route path.</param>
/// <param name="Culture">The authoritative page culture.</param>
public sealed record PageRenderMetadata(
    long? Id,
    long SiteId,
    string RendererId,
    string Title,
    string Slug,
    string Path,
    string Culture);

/// <summary>Immutable exact source selected for one source-rendered request.</summary>
/// <param name="VersionId">
/// The persisted source-version identifier, or zero for an unsaved preview.
/// </param>
/// <param name="RendererId">The renderer that owns the source.</param>
/// <param name="Source">The exact source text.</param>
/// <param name="SourceHash">The lowercase hexadecimal SHA-256 hash of <paramref name="Source"/>.</param>
public sealed record PageRenderSource(
    long VersionId,
    string RendererId,
    string Source,
    string SourceHash);

/// <summary>
/// Immutable request supplied to one registered full-page rendering strategy.
/// </summary>
/// <param name="Metadata">The immutable site-owned page metadata selected by the public boundary.</param>
/// <param name="Source">The exact selected source version for source renderers, when applicable.</param>
/// <param name="Content">The selected draft or published HTML snapshot.</param>
/// <param name="Composition">The matching typed-composition snapshot.</param>
/// <param name="ContentPageNumbers">Bounded content-list page numbers resolved from declared query keys.</param>
/// <param name="ContentQueries">Eager named hierarchy results resolved before renderer dispatch.</param>
/// <param name="IsPreview">Whether draft content and query results are permitted.</param>
public sealed record PageRenderRequest(
    PageRenderMetadata Metadata,
    PageRenderSource? Source,
    HtmlPageContent Content,
    PageCompositionDocument? Composition,
    IReadOnlyDictionary<long, int> ContentPageNumbers,
    PageContentQueryResolution ContentQueries,
    bool IsPreview = false,
    IReadOnlyDictionary<string, string>? RouteValues = null);

/// <summary>Validated markup, CSS, and content dependencies produced by a page renderer.</summary>
/// <param name="Markup">Validated fragment markup for the deployment-owned page shell.</param>
/// <param name="CssText">Validated page-scoped CSS.</param>
/// <param name="ContentTypeAliases">Content-type aliases used while producing the result.</param>
public sealed record RenderedPage(
    string Markup,
    string CssText,
    IReadOnlyList<string> ContentTypeAliases);

/// <summary>Renders one full page without owning routing, persistence, or the public shell.</summary>
public interface IPageRenderer
{
    /// <summary>Gets the stable renderer identifier.</summary>
    PageRendererId Id { get; }

    /// <summary>Gets editor and capability metadata for this renderer.</summary>
    PageRendererDescriptor Descriptor { get; }

    /// <summary>Renders a preselected page snapshot.</summary>
    Task<Result<RenderedPage>> RenderAsync(
        PageRenderRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolves explicitly registered full-page renderer strategies.</summary>
public interface IPageRendererRegistry
{
    /// <summary>Gets the deterministic renderer catalog advertised to manager clients.</summary>
    IReadOnlyList<PageRendererDescriptor> Descriptors { get; }

    /// <summary>Resolves a renderer by its persisted stable identifier.</summary>
    Result<IPageRenderer> Resolve(string? rendererId);
}
