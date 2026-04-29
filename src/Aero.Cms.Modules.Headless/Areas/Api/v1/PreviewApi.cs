using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Web.Core.Blocks.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

/// <summary>
/// Admin API for previewing unpublished content.
/// </summary>
public static class PreviewApi
{
    /// <summary>
    /// Maps the Preview API endpoints.
    /// </summary>
    public static void MapPreviewApi(this IEndpointRouteBuilder app)
    {
        app.MapGet($"/{HttpConstants.ApiPrefix}admin/preview/pages/{{id:long}}", PreviewPage)
            .WithName("PreviewPage")
            .WithTags("Admin - Preview");

        app.MapGet($"/{HttpConstants.ApiPrefix}admin/preview/blog-posts/{{id:long}}", PreviewBlogPost)
            .WithName("PreviewBlogPost")
            .WithTags("Admin - Preview");

        app.MapPost($"/{HttpConstants.ApiPrefix}admin/preview/blocks/render-fragment", PreviewBlockFragment)
            .WithName("PreviewBlockFragment")
            .WithTags("Admin - Preview");
    }

    private static async Task<IResult> PreviewPage(
        long id,
        IPageContentService pageService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PreviewApi));
        try
        {
            var result = await pageService.LoadAsync(id, cancellationToken);

            if (result is Result<PageDocument?, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to preview page id={Id}: {Error}", id, failure.Error);
                return TypedResults.NotFound(new { error = failure.Error });
            }

            if (result is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(new PreviewResponse<PageDocument>(ok.Value, "page"));
            }

            return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error previewing page id={Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> PreviewBlogPost(
        long id,
        IBlogPostContentService blogService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PreviewApi));
        try
        {
            var result = await blogService.LoadAsync(id, cancellationToken);

            if (result is Result<BlogPostDocument?, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to preview blog post id={Id}: {Error}", id, failure.Error);
                return TypedResults.NotFound(new { error = failure.Error });
            }

            if (result is Result<BlogPostDocument?, AeroError>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(new PreviewResponse<BlogPostDocument>(ok.Value, "blog-post"));
            }

            return TypedResults.NotFound(new { error = $"Blog post with id '{id}' not found." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error previewing blog post id={Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> PreviewBlockFragment(
        [FromBody] PreviewBlockFragmentRequest request,
        CmsBlockHtmlRenderer blockRenderer,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PreviewApi));

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

/// <summary>
/// Response wrapper for preview content.
/// </summary>
/// <param name="Content">The content document being previewed.</param>
/// <param name="ContentType">The type of content (page or blog-post).</param>
/// <param name="IsDraft">Whether the content is in draft state.</param>
public record PreviewResponse<T>(T Content, string ContentType) where T : class
{
    public bool IsDraft => Content switch
    {
        PageDocument page => page.PublicationState == ContentPublicationState.Draft,
        BlogPostDocument post => post.PublicationState == ContentPublicationState.Draft,
        _ => true
    };
}

public sealed record PreviewBlockFragmentRequest(BlockBase? Block);

public sealed record PreviewBlockFragmentResponse(string Html);
