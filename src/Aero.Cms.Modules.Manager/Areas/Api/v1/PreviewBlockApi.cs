using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Web.Core.Blocks.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Manager.Areas.Api.v1;

/// <summary>
/// Cross-cutting block fragment preview — works for any block type,
/// used by both PageEditor and PostEditor preview overlays.
/// Moved from HeadlessModule to ManagerModule (Phase 4).
/// </summary>
public static class PreviewBlockApi
{
    public static void MapPreviewBlockFragmentApi(this IEndpointRouteBuilder app)
    {
        app.MapPost($"/{HttpConstants.ApiPrefix}admin/preview/blocks/render-fragment", PreviewBlockFragment)
            .WithName("PreviewBlockFragment")
            .WithTags("Admin - Preview");
    }

    private static async Task<IResult> PreviewBlockFragment(
        [FromBody] PreviewBlockFragmentRequest request,
        CmsBlockHtmlRenderer blockRenderer,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PreviewBlockApi));

        try
        {
            if (request.Block is null)
            {
                return TypedResults.BadRequest(new { error = "A block payload is required." });
            }

            var html = await blockRenderer.RenderAsync(request.Block, cancellationToken: cancellationToken);
            return TypedResults.Ok(new PreviewBlockFragmentResponse(RenderHtmlContent(html)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rendering preview block fragment");
            return TypedResults.Json(new { error = "An error occurred rendering the preview fragment." }, statusCode: 500);
        }
    }

    private static string RenderHtmlContent(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
