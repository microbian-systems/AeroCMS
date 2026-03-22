namespace Aero.Cms.Modules.Admin.Api;

using Aero.Cms.Modules.Blog;
using Aero.Cms.Modules.Pages;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin API for publishing and unpublishing content.
/// </summary>
public static class PublishApi
{
    /// <summary>
    /// Maps the Publish API endpoints.
    /// </summary>
    public static void MapPublishApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/admin/publish/pages/{id:long}", PublishPage)
            .WithName("PublishPage")
            .WithTags("Admin - Publish");

        app.MapPost("/api/v1/admin/publish/blog-posts/{id:long}", PublishBlogPost)
            .WithName("PublishBlogPost")
            .WithTags("Admin - Publish");

        app.MapPost("/api/v1/admin/unpublish/pages/{id:long}", UnpublishPage)
            .WithName("UnpublishPage")
            .WithTags("Admin - Publish");

        app.MapPost("/api/v1/admin/unpublish/blog-posts/{id:long}", UnpublishBlogPost)
            .WithName("UnpublishBlogPost")
            .WithTags("Admin - Publish");
    }

    private static async Task<IResult> PublishPage(
        long id,
        IDocumentSession session,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PublishApi));
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null)
            {
                return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });
            }

            var now = DateTimeOffset.UtcNow;
            page.PublicationState = ContentPublicationState.Published;
            page.PublishedOn = page.PublishedOn ?? now;
            page.ModifiedOn = now;

            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Published page id={Id}, slug={Slug}", id, page.Slug);
            return TypedResults.Ok(new PublishResponse(page.Id, "page", ContentPublicationState.Published, page.PublishedOn));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing page id={Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> PublishBlogPost(
        long id,
        IDocumentSession session,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PublishApi));
        try
        {
            var post = await session.LoadAsync<BlogPostDocument>(id, cancellationToken);

            if (post is null)
            {
                return TypedResults.NotFound(new { error = $"Blog post with id '{id}' not found." });
            }

            var now = DateTimeOffset.UtcNow;
            post.PublicationState = ContentPublicationState.Published;
            post.PublishedOn = post.PublishedOn ?? now;

            session.Store(post);
            await session.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Published blog post id={Id}, slug={Slug}", id, post.Slug);
            return TypedResults.Ok(new PublishResponse(post.Id, "blog-post", ContentPublicationState.Published, post.PublishedOn));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing blog post id={Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> UnpublishPage(
        long id,
        IDocumentSession session,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PublishApi));
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null)
            {
                return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });
            }

            var now = DateTimeOffset.UtcNow;
            page.PublicationState = ContentPublicationState.Draft;
            page.PublishedOn = null;
            page.ModifiedOn = now;

            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Unpublished page id={Id}, slug={Slug}", id, page.Slug);
            return TypedResults.Ok(new PublishResponse(page.Id, "page", ContentPublicationState.Draft, null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unpublishing page id={Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> UnpublishBlogPost(
        long id,
        IDocumentSession session,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PublishApi));
        try
        {
            var post = await session.LoadAsync<BlogPostDocument>(id, cancellationToken);

            if (post is null)
            {
                return TypedResults.NotFound(new { error = $"Blog post with id '{id}' not found." });
            }

            post.PublicationState = ContentPublicationState.Draft;
            post.PublishedOn = null;

            session.Store(post);
            await session.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Unpublished blog post id={Id}, slug={Slug}", id, post.Slug);
            return TypedResults.Ok(new PublishResponse(post.Id, "blog-post", ContentPublicationState.Draft, null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unpublishing blog post id={Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }
}

/// <summary>
/// Response returned after publishing or unpublishing content.
/// </summary>
/// <param name="Id">The ID of the content.</param>
/// <param name="ContentType">The type of content (page or blog-post).</param>
/// <param name="PublicationState">The new publication state.</param>
/// <param name="PublishedOn">The timestamp when published (null if unpublished).</param>
public record PublishResponse(long Id, string ContentType, ContentPublicationState PublicationState, DateTimeOffset? PublishedOn);
