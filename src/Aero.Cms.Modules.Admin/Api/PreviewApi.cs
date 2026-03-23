namespace Aero.Cms.Modules.Admin.Api;

using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

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
        app.MapGet("/api/v1/admin/preview/pages/{id:long}", PreviewPage)
            .WithName("PreviewPage")
            .WithTags("Admin - Preview");

        app.MapGet("/api/v1/admin/preview/blog-posts/{id:long}", PreviewBlogPost)
            .WithName("PreviewBlogPost")
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

            if (result is Result<string, PageDocument?>.Failure failure)
            {
                logger.LogWarning("Failed to preview page id={Id}: {Error}", id, failure.Error);
                return TypedResults.NotFound(new { error = failure.Error });
            }

            if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
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

            if (result is Result<string, BlogPostDocument?>.Failure failure)
            {
                logger.LogWarning("Failed to preview blog post id={Id}: {Error}", id, failure.Error);
                return TypedResults.NotFound(new { error = failure.Error });
            }

            if (result is Result<string, BlogPostDocument?>.Ok { Value: not null } ok)
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
