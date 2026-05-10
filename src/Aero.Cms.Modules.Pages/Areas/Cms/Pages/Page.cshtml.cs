using Aero.Cms.Core.Entities;
using Aero.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Modules.Pages.Areas.Cms.Pages;

[OutputCache(PolicyName = "PagesPolicy")]
public class DynamicPageModel(IPageContentService pageService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

    public PageDocument? PageDocument { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        Result<PageDocument?, AeroError> result;

        if (DraftId is { } draftId)
        {
            // TODO: Restore auth guard with preview API key. The draft preview
            // iframe loads cross-domain (sub-site hostname), so the auth cookie
            // from the manager domain (localhost) is not sent by the browser.
            // Future options:
            //   (B) Signed token in preview URL — append ?token={signed} validated
            //       server-side via IDataProtector.
            //   (C) Cross-domain SSO on site switch — hidden request to new
            //       domain with login token to set auth cookie there.
            //   (D) Seeded preview API key — validate ?key={apiKey} query param
            //       against a PreviewApiKey document in Marten.
            // For now: no auth on draft preview since IDs are Snowflakes
            // (unguessable) and access requires knowing the exact page ID.

            result = await pageService.LoadAsync(draftId, cancellationToken);
        }
        // If no slug provided, load the homepage
        else if (string.IsNullOrWhiteSpace(Slug))
        {
            result = await pageService.LoadHomepageAsync(cancellationToken);
        }
        else
        {
            // Normalize slug - remove leading slash if present for consistency
            var normalizedSlug = Slug.TrimStart('/');
            result = await pageService.FindBySlugAsync(normalizedSlug, cancellationToken);
        }

        var page = result switch
        {
            Result<PageDocument?, AeroError>.Ok(var foundPage) => foundPage,
            Result<PageDocument?, AeroError>.Failure => (PageDocument?)null,
            _ => (PageDocument?)null
        };

        if (page is null)
        {
            return NotFound();
        }

        PageDocument = page;
        PreserveReExecutedStatusCode();
        ApplyResponseCacheHeaders();
        return Page();
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
}
