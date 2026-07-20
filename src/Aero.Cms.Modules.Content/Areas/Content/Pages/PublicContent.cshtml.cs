using Aero.Cms.Modules.Content.Rendering;
using Aero.Cms.Shared.Localization;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using System.Globalization;

namespace Aero.Cms.Modules.Content.Areas.Content.Pages;

/// <summary>
/// Resolves and renders a public runtime-defined content item for the current site and UI culture.
/// </summary>
/// <param name="siteContext">The current site boundary used for content lookup and cache metadata.</param>
/// <param name="renderer">The service that resolves and renders published content.</param>
/// <param name="logger">The logger for not-found diagnostics and rendering failures.</param>
/// <remarks>
/// Reserved route prefixes and invalid normalized values return 404 before lookup. Rendered HTML is
/// trusted template output and is not sanitized by this page model.
/// </remarks>
[OutputCache(PolicyName = "ContentPublicPolicy")]
public sealed class PublicContentModel(
    ISiteContext siteContext,
    ContentTypeUrlRenderer renderer,
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

        /// <summary>
    /// Renders the requested public content route and populates output-cache metadata.
    /// </summary>
    /// <param name="typeAlias">The route type segment, with any leading culture stripped.</param>
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
        string typeAlias,
        string entrySlug,
        CancellationToken cancellationToken)
    {
        if (ReservedPrefixes.Contains(typeAlias))
            return NotFound();

        var normalizedType = AeroCultureRoute.StripLeadingCulture(typeAlias);
        var normalizedSlug = entrySlug.Trim().Trim('/').TrimEnd('.');
        if (string.IsNullOrWhiteSpace(normalizedType) || string.IsNullOrWhiteSpace(normalizedSlug))
            return NotFound();

        try
        {
            var result = await renderer.RenderAsync(
                siteContext.SiteId,
                normalizedType,
                CultureInfo.CurrentUICulture.Name,
                normalizedSlug,
                cancellationToken);

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
            HttpContext.Items["AeroCms.SiteId"] = siteContext.SiteId;
            HttpContext.Items["AeroCms.ContentItemId"] = ok.Value.ItemId;
            HttpContext.Items["AeroCms.ContentTypeAlias"] = normalizedType;
            HttpContext.Items["AeroCms.ContentItemSlug"] = normalizedSlug;
            HttpContext.Items["AeroCms.ContentCulture"] = ok.Value.Culture;
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
}
