using System.Security.Claims;
using Aero.Cms.Core.Audit;
using Aero.Cms.Modules.Blog.Models;
using Aero.Cms.Modules.Blog.Requests;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Api.v1;

[Obsolete("only here for ref purposes", true)]
public static class BlogApiV2
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

        app.MapPost("/api/v1/blog/posts", CreatePost)
            .WithName("CreatePost")
            .WithTags("Blog");

        app.MapPut("/api/v1/blog/posts/{id}", UpdatePost)
            .WithName("UpdatePost")
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

    private static async Task<IResult> CreatePost(
        [FromBody] CreateBlogPostRequest request,
        IBlogPostContentService blogService,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check slug uniqueness
            var normalizedSlug = ContentSlugDocument.Normalize(request.Slug);
            var existingSlug = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(s => s.NormalizedSlug == normalizedSlug, cancellationToken);
            if (existingSlug != null)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Slug already exists",
                    Detail = $"The slug '{request.Slug}' is already reserved by another post"
                });
            }

            var post = new BlogPostDocument
            {
                Id = Snowflake.NewId(),
                Title = request.Title,
                Slug = request.Slug,
                Excerpt = request.Summary,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                ImageUrl = request.ImageUrl,
                PublicationState = request.PublicationState,
                Content = []
            };

            var result = await blogService.SaveAsync(post, cancellationToken);

            if (result is Result<string, BlogPostDocument>.Failure failure)
            {
                return TypedResults.BadRequest(new { error = failure.Error });
            }

            if (result is Result<string, BlogPostDocument>.Ok ok)
            {
                var userId = GetUserId(httpContextAccessor);
                var auditEvent = BlogPostCreatedEvent.Create(userId, post.Id, post.Title, post.Slug, null);
                await auditService.LogAsync(auditEvent, cancellationToken);
                return TypedResults.Ok(ok.Value);
            }

            return TypedResults.BadRequest(new { error = "An unexpected error occurred." });
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdatePost(
        long id,
        [FromBody] UpdateBlogPostRequest request,
        IBlogPostContentService blogService,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check slug uniqueness (excluding current post)
            var normalizedSlug = ContentSlugDocument.Normalize(request.Slug);
            var existingSlug = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(s => s.NormalizedSlug == normalizedSlug && s.OwnerId != id, cancellationToken);
            if (existingSlug != null)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Slug already exists",
                    Detail = $"The slug '{request.Slug}' is already reserved by another post"
                });
            }

            var loadResult = await blogService.LoadAsync(id, cancellationToken);

            if (loadResult is Result<string, BlogPostDocument?>.Failure failure)
            {
                return TypedResults.NotFound(new { error = failure.Error });
            }

            if (loadResult is Result<string, BlogPostDocument?>.Ok { Value: null })
            {
                return TypedResults.NotFound(new { error = $"Blog post with id '{id}' not found" });
            }

            if (loadResult is not Result<string, BlogPostDocument?>.Ok { Value: not null } ok)
            {
                return TypedResults.NotFound(new { error = $"Blog post with id '{id}' not found" });
            }

            var existingPost = ok.Value;

            existingPost.Title = request.Title;
            existingPost.Slug = request.Slug;
            existingPost.Excerpt = request.Summary;
            existingPost.SeoTitle = request.SeoTitle;
            existingPost.SeoDescription = request.SeoDescription;
            existingPost.ImageUrl = request.ImageUrl;
            existingPost.PublicationState = request.PublicationState;

            var saveResult = await blogService.SaveAsync(existingPost, cancellationToken);

            if (saveResult is Result<string, BlogPostDocument>.Failure saveFailure)
            {
                return TypedResults.BadRequest(new { error = saveFailure.Error });
            }

            if (saveResult is Result<string, BlogPostDocument>.Ok saveOk)
            {
                var userId = GetUserId(httpContextAccessor);
                var auditEvent = BlogPostUpdatedEvent.Create(userId, existingPost.Id, existingPost.Title, existingPost.Slug);
                await auditService.LogAsync(auditEvent, cancellationToken);
                return TypedResults.Ok(saveOk.Value);
            }

            return TypedResults.BadRequest(new { error = "An unexpected error occurred." });
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static long GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return 0;
    }
}
