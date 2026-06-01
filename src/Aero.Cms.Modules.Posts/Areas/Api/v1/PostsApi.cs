using System.Security.Claims;
using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Audit;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Web.Core.Blocks.Rendering;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using CreatePostRequest = Aero.Cms.Modules.Posts.Requests.CreatePostRequest;
using UpdatePostRequest = Aero.Cms.Modules.Posts.Requests.UpdatePostRequest;

namespace Aero.Cms.Modules.Posts.Areas.Api.v1;

public static class PostsApi
{
    /// <summary>
    /// Maps the Blog API endpoints.
    /// </summary>
    public static void MapBlogApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/blogs")
            .WithTags("Admin - Blog");

        group.MapGet("/", ListPosts)
            .WithName("ListPosts");

        group.MapGet("/translation-groups", ListPostTranslationGroups)
            .WithName("ListPostTranslationGroups");

        group.MapGet("/{id:long}", GetPostById)
            .WithName("GetPostById");

        group.MapGet("/{id:long}/translations", ListPostTranslations)
            .WithName("ListPostTranslations");

        group.MapGet("/slug/{slug}", GetPostBySlug)
            .WithName("GetPostBySlug");

        group.MapPost("/", CreatePost)
            .WithName("CreatePost");

        group.MapPost("/{id:long}/translations", ForkPostToCulture)
            .WithName("ForkPostToCulture");

        group.MapPut("/{id:long}", UpdatePost)
            .WithName("UpdatePost");

        group.MapDelete("/{id:long}", DeletePost)
            .WithName("DeletePost");

        group.MapDelete("/translation-groups/{translationGroupId:long}", DeletePostTranslationGroup)
            .WithName("DeletePostTranslationGroup");

        group.MapPost("/{id:long}/publish", PublishPost)
            .WithName("PublishPost");

        group.MapPost("/{id:long}/unpublish", UnpublishPost)
            .WithName("UnpublishPost");

        group.MapPost("/import", ImportPosts)
            .WithName("ImportPosts");

        // Preview endpoints (moved from Headless PreviewApi)
        app.MapGet($"/{HttpConstants.ApiPrefix}admin/preview/blog-posts/{{id:long}}", PreviewBlogPost)
            .WithName("PreviewBlogPost")
            .WithTags("Admin - Preview");

        app.MapPost($"/{HttpConstants.ApiPrefix}admin/preview/blog-posts/render-fragment", PreviewBlogPostFragment)
            .WithName("PreviewBlogPostFragment")
            .WithTags("Admin - Preview");
    }

    private static async Task<IResult> ListPosts(
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PostsApi));
        try
        {
            var siteId = siteContext.SiteId;
            var (items, totalCount) = await postsActor.GetAllPostsAsync(siteId, skip, take, search, cancellationToken);

            var summaries = items.Select(p => new BlogSummary(
                p.Id,
                p.Title ?? string.Empty,
                p.Slug ?? string.Empty,
                p.CreatedOn.DateTime,
                p.PublishedOn?.DateTime,
                p.Excerpt,
                p.ImageUrl
            )).ToList();

            return TypedResults.Ok(new PagedResult<BlogSummary>(summaries, totalCount, skip, take));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving blog posts");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> ListPostTranslationGroups(
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? culture = null,
        CancellationToken cancellationToken = default)
    {
        var site = await query.LoadAsync<SitesModel>(siteContext.SiteId, cancellationToken);
        var defaultCulture = ContentSlugDocument.NormalizeCulture(site?.DefaultCulture ?? SitesModel.DefaultCultureName);
        var selectedCulture = string.IsNullOrWhiteSpace(culture)
            ? defaultCulture
            : ContentSlugDocument.NormalizeCulture(culture);

        var posts = await query.Query<PostDocument>()
            .Where(x => x.SiteId == siteContext.SiteId)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            posts = posts
                .Where(x => x.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || x.Slug.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || x.Culture.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (x.Excerpt?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        var groups = posts
            .GroupBy(x => x.TranslationGroupId ?? x.Id)
            .Select(x => MapToTranslationGroupSummary(x.Key, x.ToList(), defaultCulture, selectedCulture))
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = groups.Skip(skip).Take(take).ToList();
        return TypedResults.Ok(new PagedResult<BlogTranslationGroupSummary>(items, groups.Count, skip, take));
    }

    private static async Task<IResult> GetPostById(
        long id,
        [FromServices] IAeroPostActor postsActor,
        CancellationToken cancellationToken = default)
    {
        var result = await postsActor.GetByIdAsync(id, cancellationToken);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(MapToBlogDetail(result.data));
    }

    private static async Task<IResult> ListPostTranslations(
        long id,
        [FromServices] IAeroPostActor postsActor,
        CancellationToken cancellationToken = default)
    {
        var variants = await postsActor.ListCultureVariantsAsync(id, cancellationToken);
        return TypedResults.Ok(variants.Select(MapToBlogDetail).ToList());
    }

    private static async Task<IResult> ForkPostToCulture(
        long id,
        [FromBody] ForkBlogCultureRequest request,
        [FromServices] IAeroPostActor postsActor,
        CancellationToken cancellationToken = default)
    {
        var result = await postsActor.ForkPostForCultureAsync(id, request.Culture, request.Slug, cancellationToken);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(new { error = result.error })
            : TypedResults.Ok(MapToBlogDetail(result.data));
    }

    private static async Task<IResult> GetPostBySlug(
        string slug,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var result = await postsActor.GetBySlugAsync(siteContext.SiteId, slug, cancellationToken);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound()
            : TypedResults.Ok(result.data);
    }


    // todo - the auditservice here is not fully integrated since the port to Orleans - figure out how to integrate properly

    private static async Task<IResult> CreatePost(
        [FromBody] CreatePostRequest request,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IAuditService auditService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check slug uniqueness
            var siteId = siteContext.SiteId;
            var slugCheck = await postsActor.GetBySlugAsync(siteId, request.Slug, cancellationToken);
            if (string.IsNullOrWhiteSpace(slugCheck.error.Message))
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Slug already exists",
                    Detail = $"The slug '{request.Slug}' is already reserved by another post"
                });
            }

            var vm = new PostViewModel
            {
                Id = Snowflake.NewId(),
                SiteId = siteId,
                Title = request.Title,
                Slug = request.Slug,
                Excerpt = request.Summary,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                ImageUrl = request.ImageUrl,
                PublicationState = request.PublicationState,
                // Store raw markdown string — Orleans can't serialize MarkdownBlock
                Content = string.IsNullOrWhiteSpace(request.MarkdownContent)
                    ? new List<object>()
                    : new List<object> { request.MarkdownContent },
                CreatedOn = DateTimeOffset.UtcNow,
                CreatedBy = "system",
                ModifiedBy = "system"
            };

            var result = await postsActor.SavePostAsync(vm, siteId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.error.Message))
                return TypedResults.BadRequest(new { error = result.error });

            var userId = GetUserId(httpContextAccessor);
            var auditEvent = BlogPostCreatedEvent.Create(userId, vm.Id, vm.Title ?? string.Empty, vm.Slug ?? string.Empty, null);
            await auditService.LogAsync(auditEvent, cancellationToken);
            return TypedResults.Ok(MapToBlogDetail(result.data));
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdatePost(
        long id,
        [FromBody] UpdatePostRequest request,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IAuditService auditService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var siteId = siteContext.SiteId;

            // Load existing post
            var loadResult = await postsActor.GetByIdAsync(id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(loadResult.error.Message))
                return TypedResults.NotFound(new { error = loadResult.error.Message });

            var existing = loadResult.data;

            // Check slug uniqueness (excluding current post)
            if (!string.Equals(existing.Slug, request.Slug, StringComparison.OrdinalIgnoreCase))
            {
                var slugCheck = await postsActor.GetBySlugAsync(siteId, request.Slug, cancellationToken);
                if (string.IsNullOrWhiteSpace(slugCheck.error.Message))
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Slug already exists",
                        Detail = $"The slug '{request.Slug}' is already reserved by another post"
                    });
                }
            }

            existing.Title = request.Title;
            existing.Slug = request.Slug;
            existing.Excerpt = request.Summary;
            existing.SeoTitle = request.SeoTitle;
            existing.SeoDescription = request.SeoDescription;
            existing.ImageUrl = request.ImageUrl;
            existing.PublicationState = request.PublicationState;

            // Update markdown content if provided
            if (request.MarkdownContent is not null)
            {
                existing.Content = string.IsNullOrWhiteSpace(request.MarkdownContent)
                    ? new List<object>()
                    : new List<object> { request.MarkdownContent };
            }

            var result = await postsActor.SavePostAsync(existing, siteId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.error.Message))
                return TypedResults.BadRequest(new { error = result.error });

            var userId = GetUserId(httpContextAccessor);
            var auditEvent = BlogPostUpdatedEvent.Create(userId, existing.Id, existing.Title, existing.Slug);
            await auditService.LogAsync(auditEvent, cancellationToken);
            return TypedResults.Ok(MapToBlogDetail(result.data));
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeletePost(
        long id,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] IAuditService auditService,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PostsApi));
        try
        {
            var siteId = siteContext.SiteId;

            // Load first for audit info
            var loadResult = await postsActor.GetByIdAsync(id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(loadResult.error.Message))
                return TypedResults.NotFound();

            var post = loadResult.data;
            var result = await postsActor.DeletePostAsync(id, siteId, cancellationToken);

            if (string.IsNullOrWhiteSpace(result.error.Message))
            {
                var userId = GetUserId(httpContextAccessor);
                var auditEvent = BlogPostDeletedEvent.Create(userId, post.Id, post.Title ?? string.Empty);
                await auditService.LogAsync(auditEvent, cancellationToken);
                return TypedResults.Ok(true);
            }

            return TypedResults.Problem(result.error.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting blog post for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeletePostTranslationGroup(
        long translationGroupId,
        [FromServices] IPostContentService postService,
        CancellationToken cancellationToken = default)
    {
        var result = await postService.DeleteTranslationGroupAsync(translationGroupId, cancellationToken);
        return result switch
        {
            Result<int, AeroError>.Ok ok => TypedResults.Ok(new DeleteBlogTranslationGroupResult(ok.Value)),
            Result<int, AeroError>.Failure failure => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete post translation group",
                Detail = failure.Error.ToString(),
                Status = StatusCodes.Status400BadRequest
            }),
            _ => TypedResults.Problem("Unknown delete result.")
        };
    }

    private static async Task<IResult> PublishPost(
        long id,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IAuditService auditService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var siteId = siteContext.SiteId;
            var result = await postsActor.PublishPostAsync(id, siteId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.error.Message))
                return TypedResults.BadRequest(new { error = result.error });

            var userId = GetUserId(httpContextAccessor);
            var auditEvent = BlogPostUpdatedEvent.Create(userId, result.data.Id, result.data.Title, result.data.Slug);
            await auditService.LogAsync(auditEvent, cancellationToken);
            return TypedResults.Ok(MapToBlogDetail(result.data));
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UnpublishPost(
        long id,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IAuditService auditService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var siteId = siteContext.SiteId;
            var result = await postsActor.UnpublishPostAsync(id, siteId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.error.Message))
                return TypedResults.BadRequest(new { error = result.error });

            var userId = GetUserId(httpContextAccessor);
            var auditEvent = BlogPostUpdatedEvent.Create(userId, result.data.Id, result.data.Title, result.data.Slug);
            await auditService.LogAsync(auditEvent, cancellationToken);
            return TypedResults.Ok(MapToBlogDetail(result.data));
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ImportPosts(
        [FromBody] ImportFileRequest request,
        [FromServices] IPostImportService importService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PostsApi));
        try
        {
            var result = await importService.ImportAsync(request, cancellationToken);

            if (result is Result<ImportBlogResult, AeroError>.Failure failure)
            {
                logger.LogWarning("Import failed: {Error}", failure.Error);
                return TypedResults.BadRequest(new { error = failure.Error.ToString() });
            }

            if (result is Result<ImportBlogResult, AeroError>.Ok ok)
            {
                logger.LogInformation("Import completed: {Imported} imported, {Skipped} skipped, {Errors} errors",
                    ok.Value.TotalImported, ok.Value.TotalSkipped, ok.Value.Errors.Count);
                return TypedResults.Ok(ok.Value);
            }

            return TypedResults.BadRequest(new { error = "Unexpected result from import service" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing blog posts");
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
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

    // ── Preview handlers (moved from Headless PreviewApi) ──────────────

    private static async Task<IResult> PreviewBlogPost(
        long id,
        IAeroPostActor postsActor,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PostsApi));
        try
        {
            var result = await postsActor.GetByIdAsync(id, ct);
            if (string.IsNullOrWhiteSpace(result.error.Message) && result.data.Id > 0)
                return TypedResults.Ok(new PreviewResponse<PostViewModel>(result.data, "blog-post"));

            return TypedResults.NotFound(new { error = $"Blog post with id '{id}' not found." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error previewing blog post {Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> PreviewBlogPostFragment(
        [FromBody] PreviewBlogPostFragmentRequest request,
        CmsBlockHtmlRenderer blockRenderer,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PostsApi));
        try
        {
            if (request.Content is null)
                return TypedResults.BadRequest(new { error = "Blog post content is required." });

            var html = await blockRenderer.RenderBlocksAsync(request.Content, cancellationToken: ct);
            return TypedResults.Ok(new PreviewBlogPostFragmentResponse(RenderPreviewHtml(html)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rendering preview blog post fragment");
            return TypedResults.Json(new { error = "An error occurred rendering the preview fragment." }, statusCode: 500);
        }
    }

    // ── Preview helpers ─────────────────────────────────────────────────

    private static string RenderPreviewHtml(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    // ── Mapping helpers ────────────────────────────────────────────────

    /// <summary>
    /// Maps <see cref="PostViewModel"/> (Orleans-safe, strings in Content)
    /// to <see cref="BlogDetail"/> (JSON-safe, <see cref="BlockBase"/> in Content).
    /// </summary>
    private static BlogDetail MapToBlogDetail(PostViewModel vm)
    {
        var blocks = new List<BlockBase>();
        foreach (var item in vm.Content ?? [])
        {
            if (item is string markdown && !string.IsNullOrWhiteSpace(markdown))
            {
                blocks.Add(new MarkdownBlock
                {
                    Id = Snowflake.NewId(),
                    Content = markdown,
                    Order = blocks.Count
                });
            }
        }

        return new BlogDetail(
            vm.Id,
            vm.Title ?? string.Empty,
            vm.Slug ?? string.Empty,
            vm.Excerpt,
            vm.SeoTitle,
            vm.SeoDescription,
            vm.PublishedOn,
            (int)vm.PublicationState,
            blocks,
            vm.TagIds ?? [],
            vm.CategoryIds ?? [],
            vm.AuthorId,
            vm.ImageUrl,
            vm.Likes,
            vm.CreatedOn,
            vm.ModifiedOn,
            vm.Culture,
            vm.TranslationGroupId
        );
    }

    private static BlogTranslationGroupSummary MapToTranslationGroupSummary(
        long translationGroupId,
        IReadOnlyList<PostDocument> variants,
        string defaultCulture,
        string selectedCulture)
    {
        var defaultVariant = variants.FirstOrDefault(x => CultureEquals(x.Culture, defaultCulture));
        var selectedVariant = variants.FirstOrDefault(x => CultureEquals(x.Culture, selectedCulture));
        var display = selectedVariant ?? defaultVariant ?? variants.OrderBy(x => x.Culture).First();

        return new BlogTranslationGroupSummary(
            translationGroupId,
            display.Id,
            display.Culture,
            defaultCulture,
            display.Title,
            display.Slug,
            display.CreatedOn.DateTime,
            display.PublishedOn?.DateTime,
            display.Excerpt,
            display.ImageUrl,
            defaultVariant is null,
            selectedVariant is null,
            variants
                .OrderByDescending(x => CultureEquals(x.Culture, defaultCulture))
                .ThenBy(x => x.Culture, StringComparer.OrdinalIgnoreCase)
                .Select(x => new BlogTranslationVariantSummary(
                    x.Id,
                    x.Culture,
                    x.Title,
                    x.Slug,
                    x.CreatedOn.DateTime,
                    x.PublishedOn?.DateTime,
                    CultureEquals(x.Culture, defaultCulture)))
                .ToList());
    }

    private static bool CultureEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
