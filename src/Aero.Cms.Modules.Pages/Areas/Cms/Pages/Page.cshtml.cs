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
/// Represents a class for DynamicPageModel.
/// </summary>
[OutputCache(PolicyName = "PagesPolicy")]
public class DynamicPageModel(
    IAeroPageActor pageActor,
    ISiteContext siteContext,
    IDocumentStore documentStore,
    HtmlStaticRenderer htmlRenderer,
    IStyleCompiler styleCompiler,
    IStyleProfile styleProfile,
    ILogger<DynamicPageModel> logger) : PageModel
{
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

        /// <summary>
    /// Gets or sets the Draft Id.
    /// </summary>
[BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
public string? SeoTitle { get; private set; }
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; private set; }
        /// <summary>
    /// Gets or sets the Show Header Navigation.
    /// </summary>
public bool ShowHeaderNavigation { get; private set; } = true;
        /// <summary>
    /// Gets or sets the Hide Footer.
    /// </summary>
public bool HideFooter { get; private set; }
        /// <summary>
    /// Gets or sets the Show Chat Agent.
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
    /// Gets or sets the Page Id.
    /// </summary>
public long? PageId { get; private set; }
        /// <summary>
    /// Gets or sets the Page Slug.
    /// </summary>
public string? PageSlug { get; private set; }
        /// <summary>
    /// Gets or sets the Requested Culture.
    /// </summary>
public string RequestedCulture { get; private set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the Rendered Culture.
    /// </summary>
public string RenderedCulture { get; private set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the Is Culture Fallback.
    /// </summary>
public bool IsCultureFallback { get; private set; }
        /// <summary>
    /// Gets or sets the Canonical Url.
    /// </summary>
public string CanonicalUrl { get; private set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alternate Links.
    /// </summary>
public IReadOnlyList<AlternatePageLink> AlternateLinks { get; private set; } = [];
        /// <summary>
    /// Gets or sets the Culture Switcher Links.
    /// </summary>
public IReadOnlyList<CultureSwitcherLink> CultureSwitcherLinks { get; private set; } = [];

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        AeroRequestResponse<Aero.Cms.Abstractions.Models.PageViewModel> result;
        RequestedCulture = CultureInfo.CurrentUICulture.Name;

        if (DraftId is { } draftId)
        {
            result = await pageActor.GetByIdAsync(draftId, cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(Slug))
        {
            result = await pageActor.GetBySlugAsync(siteContext.SiteId, "/", CultureInfo.CurrentUICulture.Name, cancellationToken);
        }
        else
        {
            var normalizedSlug = AeroCultureRoute.StripLeadingCulture(Slug);
            result = await pageActor.GetBySlugAsync(siteContext.SiteId, normalizedSlug, CultureInfo.CurrentUICulture.Name, cancellationToken);
        }

        if (result is null || !string.IsNullOrWhiteSpace(result.error?.Message))
        {
            return NotFound();
        }

        var vm = result.data;
        if (vm is null)
        {
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
            if (document is null)
            {
                return NotFound();
            }

            var content = DraftId is not null
                ? document.DraftContent
                : document.PublishedContent;

            if (content is null)
            {
                return NotFound();
            }

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
        var variants = await pageActor.ListCultureVariantsAsync(page.Id, cancellationToken);
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
    /// Represents a record for AlternatePageLink.
    /// </summary>
public sealed record AlternatePageLink(string Hreflang, string Href);
}
