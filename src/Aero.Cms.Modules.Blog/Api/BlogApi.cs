namespace Aero.Cms.Modules.Blog.Api;

using Aero.Core.Railway;
using Aero.Cms.Modules.Blog.Models;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class BlogApi
{
    /// <summary>
    /// Maps the Blog API endpoints.
    /// </summary>
    public static void MapBlogApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/blog/posts", ListPosts)
            .WithName("ListPosts")
            .WithTags("Blog");

        app.MapGet("/api/v1/blog/posts/{slug}", GetPostBySlug)
            .WithName("GetPostBySlug")
            .WithTags("Blog");

        app.MapGet("/api/v1/blog/posts/by-tag/{tag}", GetPostsByTag)
            .WithName("GetPostsByTag")
            .WithTags("Blog");
    }

    private static async Task<IResult> ListPosts(
        IBlogPostContentService blogService,
        ILogger logger,
        int count = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await blogService.GetLatestPostsAsync(count, cancellationToken);

            if (result is Result<string, IReadOnlyList<BlogPostDocument>>.Failure failure)
            {
                logger.LogWarning("Failed to retrieve blog posts: {Error}", failure.Error);
                return TypedResults.NotFound();
            }

            if (result is Result<string, IReadOnlyList<BlogPostDocument>>.Ok ok)
            {
                return TypedResults.Ok(ok.Value);
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving blog posts");
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> GetPostBySlug(
        string slug,
        IBlogPostContentService blogService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await blogService.FindBySlugAsync(slug, cancellationToken);

            if (result is Result<string, BlogPostDocument?>.Failure failure)
            {
                logger.LogWarning("Blog post not found for slug={Slug}: {Error}", slug, failure.Error);
                return TypedResults.NotFound();
            }

            if (result is Result<string, BlogPostDocument?>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(ok.Value);
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving blog post for slug={Slug}", slug);
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> GetPostsByTag(
        string tag,
        IBlogPostContentService blogService,
        IDocumentSession session,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // First, find the tag by slug
            var tagDocument = await session.Query<Tag>()
                .FirstOrDefaultAsync(t => t.Slug == tag, cancellationToken);

            if (tagDocument is null)
            {
                logger.LogWarning("Tag not found for slug={Tag}", tag);
                return TypedResults.NotFound();
            }

            // Then get posts by tag ID
            var result = await blogService.GetByTagAsync(tagDocument.Id, cancellationToken);

            if (result is Result<string, IReadOnlyList<BlogPostDocument>>.Failure failure)
            {
                logger.LogWarning("Failed to retrieve posts for tag={Tag}: {Error}", tag, failure.Error);
                return TypedResults.NotFound();
            }

            if (result is Result<string, IReadOnlyList<BlogPostDocument>>.Ok ok)
            {
                return TypedResults.Ok(ok.Value);
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving posts for tag={Tag}", tag);
            return TypedResults.NotFound();
        }
    }
}
