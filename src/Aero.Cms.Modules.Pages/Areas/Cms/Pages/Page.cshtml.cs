using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Shared.Components;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using System.Globalization;

namespace Aero.Cms.Modules.Pages.Areas.Cms.Pages;

/// <summary>
/// Resolves and renders the culture-aware public page or an explicitly requested
/// draft preview as static HTML and native CSS.
/// </summary>
/// <param name="pageActor">The actor used for page and culture-variant lookups.</param>
/// <param name="siteContext">The current site scope.</param>
/// <param name="documentStore">The store used to load the selected HTML snapshot.</param>
/// <param name="htmlRenderer">Renders the validated HTML tree.</param>
/// <param name="styleCompiler">Compiles page-scoped styles against the site profile.</param>
/// <param name="styleProfileResolver">Resolves the site's allowed style profile.</param>
/// <param name="logger">The page logger.</param>
/// <remarks>
/// Draft selection is controlled by <see cref="DraftId"/> and is scoped to the
/// manager-selected site. Public responses receive a five-minute cache header,
/// while previews are marked no-store.
/// </remarks>
[OutputCache(PolicyName = "PagesPolicy")]
public class DynamicPageModel(
    IAeroPageActor pageActor,
    ISiteContext siteContext,
    IDocumentStore documentStore,
    HtmlStaticRenderer htmlRenderer,
    IStyleCompiler styleCompiler,
    ISiteStyleProfileResolver styleProfileResolver,
    ILogger<DynamicPageModel> logger) : PageModel
{
    /// <summary>
    /// Gets or sets the optional catch-all slug supplied by route binding.
    /// </summary>
[BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    /// <summary>
    /// Gets or sets the page identifier used to select draft content for preview.
    /// </summary>
[BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

    /// <summary>
    /// Gets the resolved SEO title, falling back to the page title.
    /// </summary>
public string? SeoTitle { get; private set; }
    /// <summary>
    /// Gets the resolved page title.
    /// </summary>
public string? Title { get; private set; }
    /// <summary>
    /// Gets whether the layout should show header navigation.
    /// </summary>
public bool ShowHeaderNavigation { get; private set; } = true;
    /// <summary>
    /// Gets whether the layout should hide its footer.
    /// </summary>
public bool HideFooter { get; private set; }
    /// <summary>
    /// Gets whether the layout should expose the chat agent.
    /// </summary>
public bool ShowChatAgent { get; private set; } = true;
    /// <summary>
    /// Gets the rendered Living Standard HTML snapshot.
    /// </summary>
public string RenderedMarkup { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the validated, page-scoped native CSS emitted for the rendered snapshot.
    /// </summary>
public string RenderedCss { get; private set; } = string.Empty;
    /// <summary>
    /// Gets the resolved page identifier.
    /// </summary>
public long? PageId { get; private set; }
    /// <summary>
    /// Gets the resolved page slug.
    /// </summary>
public string? PageSlug { get; private set; }
    /// <summary>
    /// Gets the UI culture requested for this page.
    /// </summary>
public string RequestedCulture { get; private set; } = SitesModel.DefaultCultureName;
    /// <summary>
    /// Gets the culture of the page variant that was rendered.
    /// </summary>
public string RenderedCulture { get; private set; } = SitesModel.DefaultCultureName;
    /// <summary>
    /// Gets whether the rendered variant differs from the requested culture.
    /// </summary>
public bool IsCultureFallback { get; private set; }
    /// <summary>
    /// Gets the absolute culture-prefixed URL for the rendered variant.
    /// </summary>
public string CanonicalUrl { get; private set; } = string.Empty;
    /// <summary>
    /// Gets links for published culture variants, including <c>x-default</c> when available.
    /// </summary>
public IReadOnlyList<AlternatePageLink> AlternateLinks { get; private set; } = [];
    /// <summary>
    /// Gets culture-switcher links derived from the published alternate links.
    /// </summary>
public IReadOnlyList<CultureSwitcherLink> CultureSwitcherLinks { get; private set; } = [];

    /// <summary>
    /// Resolves the requested variant, loads the selected snapshot, compiles its styles,
    /// and renders the Razor Page.
    /// </summary>
    /// <param name="cancellationToken">The token used for actor, store, and profile operations.</param>
    /// <returns>
    /// A page result on success; HTTP 404 for lookup, document, or snapshot absence;
    /// or HTTP 500 when style-profile resolution, compilation, or rendering fails.
    /// </returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        AeroRequestResponse<Aero.Cms.Abstractions.Models.PageViewModel> result;
        RequestedCulture = CultureInfo.CurrentUICulture.Name;
        var requestedSlug = Slug ?? HttpContext.Request.RouteValues["slug"]?.ToString();

        if (DraftId is { } draftId)
        {
            result = await pageActor.GetByIdAsync(draftId, siteContext.SiteId, cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(requestedSlug))
        {
            result = await pageActor.GetBySlugAsync(siteContext.SiteId, "/", CultureInfo.CurrentUICulture.Name, cancellationToken);
        }
        else
        {
            var normalizedSlug = AeroCultureRoute.StripLeadingCulture(requestedSlug);
            result = await pageActor.GetBySlugAsync(siteContext.SiteId, normalizedSlug, CultureInfo.CurrentUICulture.Name, cancellationToken);
        }

        if (result is null || !string.IsNullOrWhiteSpace(result.error?.Message))
        {
            logger.LogWarning(
                "Public page lookup failed for SiteId={SiteId}, Slug={Slug}, Culture={Culture}: {Error}",
                siteContext.SiteId,
                requestedSlug,
                CultureInfo.CurrentUICulture.Name,
                result?.error?.Message ?? "No actor response");
            return NotFound();
        }

        var vm = result.data;
        if (vm is null)
        {
            logger.LogWarning(
                "Public page lookup returned no document for SiteId={SiteId}, Slug={Slug}, Culture={Culture}",
                siteContext.SiteId,
                requestedSlug,
                CultureInfo.CurrentUICulture.Name);
            return NotFound();
        }

        SeoTitle = vm.SeoTitle ?? vm.Title;
        Title = vm.Title;
        ShowHeaderNavigation = vm.ShowHeaderNavigation;
        HideFooter = vm.HideFooter;
        ShowChatAgent = vm.ShowChatAgent;
        PageId = vm.Id;
        PageSlug = vm.Slug;
        RenderedCulture = vm.Culture;
        IsCultureFallback = !string.Equals(RequestedCulture, RenderedCulture, StringComparison.OrdinalIgnoreCase);
        CanonicalUrl = BuildCultureUrl(RenderedCulture, vm.Slug);

        await using (var session = await documentStore.QuerySessionAsync())
        {
            var document = await session.LoadAsync<PageDocument>(vm.Id, cancellationToken);
            if (document is null || document.SiteId != siteContext.SiteId)
            {
                logger.LogWarning(
                    "Page metadata resolved to missing or cross-site PageDocument {PageId} for SiteId={SiteId}, Slug={Slug}",
                    vm.Id,
                    siteContext.SiteId,
                    requestedSlug);
                return NotFound();
            }

            var content = DraftId is not null
                ? document.DraftContent
                : document.PublishedContent;

            if (content is null)
            {
                logger.LogWarning(
                    "Public page {PageId} has no {ContentKind} HTML snapshot for Slug={Slug}",
                    vm.Id,
                    DraftId is not null ? "draft" : "published",
                    requestedSlug);
                return NotFound();
            }

            var profileResult = await styleProfileResolver.ResolveAsync(
                document.SiteId,
                cancellationToken);
            if (profileResult is Result<IStyleProfile, AeroError>.Failure profileFailure)
            {
                logger.LogError(
                    "Style-profile resolution failed for page {PageId} on site {SiteId}: {Error}",
                    vm.Id,
                    document.SiteId,
                    profileFailure.Error);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var styleProfile = ((Result<IStyleProfile, AeroError>.Ok)profileResult).Value;
            var compiled = styleCompiler.Compile(content, styleProfile);
            if (compiled is Result<CompiledPageStyles>.Failure styleFailure)
            {
                logger.LogError(
                    "Published HTML style compilation failed for page {PageId}: {Error}",
                    vm.Id,
                    styleFailure.Error);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var rendered = htmlRenderer.RenderPage(
                content,
                ((Result<CompiledPageStyles>.Ok)compiled).Value);
            if (rendered is Result<RenderedHtmlPage>.Failure renderFailure)
            {
                logger.LogError(
                    "Published HTML rendering failed for page {PageId}: {Error}",
                    vm.Id,
                    renderFailure.Error);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var renderedPage = ((Result<RenderedHtmlPage>.Ok)rendered).Value;
            RenderedMarkup = renderedPage.Markup;
            RenderedCss = renderedPage.CssText;
            HttpContext.Items["AeroCms.SiteId"] = document.SiteId;
        }

        // Store page ID + slug for output cache tagging
        HttpContext.Items["AeroCms.PageId"] = vm.Id;
        HttpContext.Items["AeroCms.PageSlug"] = vm.Slug;
        ViewData["RequestedCulture"] = RequestedCulture;
        ViewData["RenderedCulture"] = RenderedCulture;
        ViewData["IsCultureFallback"] = IsCultureFallback;
        AlternateLinks = await BuildAlternateLinksAsync(vm, cancellationToken);
        CultureSwitcherLinks = BuildCultureSwitcherLinks(AlternateLinks);
        ViewData["CultureSwitcherLinks"] = CultureSwitcherLinks;

        PreserveReExecutedStatusCode();
        ApplyResponseCacheHeaders();
        return Page();
    }

    private async Task<IReadOnlyList<AlternatePageLink>> BuildAlternateLinksAsync(
        PageViewModel page,
        CancellationToken cancellationToken)
    {
        var variants = await pageActor.ListCultureVariantsAsync(
            page.Id,
            siteContext.SiteId,
            cancellationToken);
        if (variants.Count == 0)
            variants = [page];

        var publishedVariants = variants
            .Where(variant => variant.IsPublished)
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Culture))
            .GroupBy(variant => variant.Culture, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (publishedVariants.Count == 0 && page.IsPublished)
            publishedVariants.Add(page);

        var links = publishedVariants
            .Select(variant => new AlternatePageLink(
                variant.Culture.ToLowerInvariant(),
                BuildCultureUrl(variant.Culture, variant.Slug)))
            .ToList();

        var siteDefaultCulture = HttpContext.Features.Get<IAeroSiteSlice>()?.DefaultCulture
            ?? page.Culture
            ?? SitesModel.DefaultCultureName;
        var defaultVariant = publishedVariants.FirstOrDefault(variant =>
            string.Equals(variant.Culture, siteDefaultCulture, StringComparison.OrdinalIgnoreCase));

        if (defaultVariant is not null)
        {
            links.Add(new AlternatePageLink("x-default", BuildCultureUrl(defaultVariant.Culture, defaultVariant.Slug)));
        }

        return links;
    }

    private IReadOnlyList<CultureSwitcherLink> BuildCultureSwitcherLinks(IReadOnlyList<AlternatePageLink> alternateLinks)
        => alternateLinks
            .Where(link => !string.Equals(link.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase))
            .Select(link => CultureSwitcher.CreateLink(
                link.Hreflang,
                link.Href,
                string.Equals(link.Hreflang, RequestedCulture, StringComparison.OrdinalIgnoreCase)
                || string.Equals(link.Hreflang, RenderedCulture, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(link => link.Hreflang, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

    private string BuildCultureUrl(string culture, string? slug)
    {
        var normalizedSlug = (slug ?? string.Empty).Trim().Trim('/');
        var path = string.IsNullOrWhiteSpace(normalizedSlug)
            ? $"/{culture.ToLowerInvariant()}"
            : $"/{culture.ToLowerInvariant()}/{normalizedSlug}";

        return UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, path);
    }

    private void PreserveReExecutedStatusCode()
    {
        var reExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        if (reExecuteFeature is null)
        {
            return;
        }

        if (reExecuteFeature.OriginalStatusCode is >= 400 and <= 599)
        {
            Response.StatusCode = reExecuteFeature.OriginalStatusCode;
        }
    }

    private void ApplyResponseCacheHeaders()
    {
        if (DraftId is not null)
        {
            Response.Headers.CacheControl = "no-store, no-cache";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            return;
        }

        Response.Headers.CacheControl = "public,max-age=300";
    }

    /// <summary>
    /// Describes an alternate-language page URL for HTML link metadata.
    /// </summary>
    /// <param name="Hreflang">The lower-case culture tag or <c>x-default</c>.</param>
    /// <param name="Href">The absolute culture-prefixed URL.</param>
public sealed record AlternatePageLink(string Hreflang, string Href);
}
