using Aero.Cms.Modules.Content.Rendering;
using Aero.Cms.Shared.Localization;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Content.Areas.Content.Pages;

/// <summary>
/// Resolves and renders a public runtime-defined content item for the current site and UI culture.
/// </summary>
/// <param name="siteContext">The current site boundary used for content lookup and cache metadata.</param>
/// <param name="renderer">The service that resolves and renders published content.</param>
/// <param name="queryService">The service that enumerates published translation variants for alternate links.</param>
/// <param name="logger">The logger for not-found diagnostics and rendering failures.</param>
/// <remarks>
/// Reserved route prefixes and invalid normalized values return 404 before lookup. Rendered HTML is
/// trusted template output and is not sanitized by this page model.
/// </remarks>
[OutputCache(PolicyName = "ContentPublicPolicy")]
[AllowAnonymous]
public sealed class PublicContentModel(
    ISiteContext siteContext,
    ContentTypeUrlRenderer renderer,
    IContentQueryService queryService,
    ILogger<PublicContentModel> logger) : PageModel
{
    private static readonly HashSet<string> ReservedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "manager", "api", "account", "login", "logout",
        "register", "health", "swagger", "scalar", "favicon.ico"
    };

        /// <summary>
    /// Gets the display title derived from the normalized slug after a successful render.
    /// </summary>
public string Title { get; private set; } = "Content";

        /// <summary>
    /// Gets renderer-produced HTML for raw emission by the associated Razor Page.
    /// </summary>
public string RenderedHtml { get; private set; } = string.Empty;
public string RequestedCulture { get; private set; } = string.Empty;
public string RenderedCulture { get; private set; } = string.Empty;
public string CanonicalUrl { get; private set; } = string.Empty;
public IReadOnlyList<AlternateContentLink> AlternateLinks { get; private set; } = [];

        /// <summary>
    /// Renders the requested public content route and populates output-cache metadata.
    /// </summary>
    /// <param name="culture">The requested route culture, or null for a legacy convenience route.</param>
    /// <param name="typeAlias">The route type segment.</param>
    /// <param name="entrySlug">The route slug trimmed of whitespace, slashes, and trailing periods.</param>
    /// <param name="cancellationToken">The token propagated to lookup and rendering.</param>
    /// <returns>
    /// The page on success, HTTP 404 for rejected or unresolved routes, or HTTP 500 for any caught
    /// exception, including cancellation.
    /// </returns>
    /// <remarks>
    /// On success, site, item, type, slug, and culture values are written to
    /// <see cref="HttpContext.Items"/> for output-cache tagging.
    /// </remarks>
public async Task<IActionResult> OnGetAsync(
        string? culture,
        string typeAlias,
        string entrySlug,
        CancellationToken cancellationToken)
    {
        if (ReservedPrefixes.Contains(culture ?? string.Empty) || ReservedPrefixes.Contains(typeAlias))
            return NotFound();

        var slice = HttpContext.Features.Get<IAeroSiteSlice>();
        var requestedAlias = culture ?? Request.Query["lang"].FirstOrDefault() ?? slice?.DefaultCulture;
        if (slice is null || !AeroCultureRoute.TryResolveSupportedCultureAlias(requestedAlias, slice.SupportedCultures, out var requestedCulture))
            return NotFound();
        var normalizedType = typeAlias.Trim().Trim('/');
        var normalizedSlug = entrySlug.Trim().Trim('/').TrimEnd('.');
        if (string.IsNullOrWhiteSpace(normalizedType) || string.IsNullOrWhiteSpace(normalizedSlug))
            return NotFound();

        var canonicalRequestPath = AeroCultureRoute.BuildCulturePath(requestedCulture, $"{normalizedType}/{normalizedSlug}");
        if (!string.Equals(Request.Path.Value, canonicalRequestPath, StringComparison.Ordinal))
            return RedirectPermanent(AeroCultureRoute.BuildCulturePathForCurrentRequest(HttpContext, requestedCulture, $"{normalizedType}/{normalizedSlug}"));

        try
        {
            var result = await renderer.RenderAsync(
                siteContext.SiteId,
                normalizedType,
                requestedCulture,
                normalizedSlug,
                cancellationToken,
                slice.DefaultCulture,
                slice.SupportedCultures);

            if (result is not Result<PublicContentRenderResult, AeroError>.Ok ok)
            {
                logger.LogInformation(
                    "Content type page {Type}/{Slug} was not rendered.",
                    normalizedType,
                    normalizedSlug);
                return NotFound();
            }

            Title = normalizedSlug.Replace('-', ' ');
            RenderedHtml = ok.Value.Html;
            RequestedCulture = ok.Value.RequestedCulture;
            RenderedCulture = ok.Value.RenderedCulture;
            CanonicalUrl = BuildAbsoluteContentUrl(RenderedCulture, normalizedType, normalizedSlug);
            var variants = await queryService.ListCultureVariantsAsync(siteContext.SiteId, normalizedType, ok.Value.TranslationGroupId, cancellationToken);
            if (variants is Result<IReadOnlyList<ContentItem>, AeroError>.Ok variantResult)
            {
                var publishedVariants = variantResult.Value
                    .Where(item => item.PublicationState == ContentPublicationState.Published)
                    .ToArray();
                var defaultVariant = publishedVariants.FirstOrDefault(item =>
                    string.Equals(item.Culture, slice.DefaultCulture, StringComparison.OrdinalIgnoreCase));
                var defaultHref = BuildAbsoluteContentUrl(
                    defaultVariant?.Culture ?? RenderedCulture,
                    normalizedType,
                    defaultVariant?.Slug ?? normalizedSlug);
                AlternateLinks = publishedVariants
                    .Select(item => new AlternateContentLink(item.Culture, BuildAbsoluteContentUrl(item.Culture, normalizedType, item.Slug)))
                    .Append(new AlternateContentLink("x-default", defaultHref))
                    .GroupBy(link => link.Hreflang, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToArray();
            }
            var renderedCultureInfo = CultureInfo.GetCultureInfo(RenderedCulture);
            ViewData["DocumentCulture"] = renderedCultureInfo.Name;
            ViewData["DocumentDirection"] = renderedCultureInfo.TextInfo.IsRightToLeft ? "rtl" : "ltr";
            if (!string.Equals(RequestedCulture, RenderedCulture, StringComparison.OrdinalIgnoreCase))
            {
                HttpContext.Items[AeroCultureRoute.IsFallbackCultureItemKey] = true;
                ViewData["IsCultureFallback"] = true;
                ViewData["RequestedCulture"] = RequestedCulture;
                ViewData["RenderedCulture"] = RenderedCulture;
            }
            HttpContext.Items["AeroCms.SiteId"] = siteContext.SiteId;
            HttpContext.Items["AeroCms.ContentItemId"] = ok.Value.ItemId;
            HttpContext.Items["AeroCms.ContentTypeAlias"] = normalizedType;
            HttpContext.Items["AeroCms.ContentItemSlug"] = normalizedSlug;
            HttpContext.Items["AeroCms.ContentCulture"] = ok.Value.RenderedCulture;
            return Page();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled error rendering content type page for site {SiteId}, type {Type}, slug {Slug}.",
                siteContext.SiteId,
                normalizedType,
                normalizedSlug);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private string BuildAbsoluteContentUrl(string culture, string typeAlias, string slug) =>
        UriHelper.BuildAbsolute(
            Request.Scheme,
            Request.Host,
            Request.PathBase,
            AeroCultureRoute.BuildCulturePath(culture, $"{typeAlias}/{slug}"));
}

public sealed record AlternateContentLink(string Hreflang, string Href);
