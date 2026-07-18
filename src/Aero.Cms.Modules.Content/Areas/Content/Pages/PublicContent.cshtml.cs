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
/// Represents a class for PublicContentModel.
/// </summary>
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
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; private set; } = "Content";

        /// <summary>
    /// Gets or sets the Rendered Html.
    /// </summary>
public string RenderedHtml { get; private set; } = string.Empty;

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
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
