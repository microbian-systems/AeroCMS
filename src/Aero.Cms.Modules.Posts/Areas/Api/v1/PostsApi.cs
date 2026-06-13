using Aero.Cms.Abstractions.Ai;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Audit;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Enums;
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

        group.MapPost("/{id:long}/ai-translate", TranslatePostWithAi)
            .WithName("TranslatePostWithAi");

        group.MapPut("/{id:long}", UpdatePost)
            .WithName("UpdatePost");

        group.MapDelete("/{id:long}", DeletePost)
            .WithName("DeletePost");

        group.MapDelete("/translation-groups/{translationGroupId:long}", DeletePostTranslationGroup)
            .WithName("DeletePostTranslationGroup");

        group.MapPost("/translation-groups/{translationGroupId:long}/publish", PublishPostTranslationGroup)
            .WithName("PublishPostTranslationGroup");

        group.MapPost("/translation-groups/{translationGroupId:long}/unpublish", UnpublishPostTranslationGroup)
            .WithName("UnpublishPostTranslationGroup");

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

    private static async Task<IResult> TranslatePostWithAi(
        long id,
        [FromBody] AiTranslateBlogRequest request,
        [FromServices] IPostContentService postService,
        [FromServices] IQuerySession query,
        [FromServices] IAiContentTranslationService translationService,
        CancellationToken cancellationToken = default)
    {
        if (request.Targets.Count == 0)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "No target cultures",
                Detail = "At least one target culture is required."
            });
        }

        var sourceResult = await postService.LoadAsync(id, cancellationToken);
        if (sourceResult is not Result<PostDocument?, AeroError>.Ok { Value: not null } sourceOk)
        {
            return TypedResults.NotFound(new { error = "Source post was not found." });
        }

        var source = sourceOk.Value;
        var site = await query.LoadAsync<SitesModel>(source.SiteId, cancellationToken);
        var supportedCultures = GetSupportedCultures(site);
        var groupId = source.TranslationGroupId ?? source.Id;
        var variantsResult = await postService.ListCultureVariantsAsync(groupId, cancellationToken);
        var variants = variantsResult is Result<IReadOnlyList<PostDocument>, AeroError>.Ok variantsOk
            ? variantsOk.Value
            : [source];

        var immediateResults = new List<AiTranslateBlogCultureResult>();
        var plans = new List<AiTranslatePostPlan>();
        var plannedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in request.Targets)
        {
            var culture = ContentSlugDocument.NormalizeCulture(target.Culture);
            if (!plannedCultures.Add(culture))
            {
                continue;
            }

            if (CultureEquals(culture, source.Culture))
            {
                immediateResults.Add(FailedTranslation(culture, "Target culture must be different from the source culture."));
                continue;
            }

            if (!supportedCultures.Contains(culture))
            {
                immediateResults.Add(FailedTranslation(culture, $"Culture '{culture}' is not supported by this site."));
                continue;
            }

            var existing = variants.FirstOrDefault(x => CultureEquals(x.Culture, culture));
            if (existing is not null && !request.OverwriteExisting)
            {
                immediateResults.Add(FailedTranslation(culture, $"A '{culture}' translation already exists."));
                continue;
            }

            var slug = string.IsNullOrWhiteSpace(target.Slug)
                ? BuildDefaultLocalizedSlug(source.Slug, culture)
                : target.Slug.Trim().Trim('/');

            plans.Add(new AiTranslatePostPlan(culture, slug, existing));
        }

        var translatedPlans = await Task.WhenAll(plans.Select(plan =>
            TranslatePostPlanAsync(source, plan, request.ProviderId, translationService, cancellationToken)));

        var results = new List<AiTranslateBlogCultureResult>(immediateResults);
        foreach (var translated in translatedPlans)
        {
            if (!translated.Succeeded || translated.Response is null)
            {
                results.Add(FailedTranslation(translated.Culture, translated.Error ?? "AI translation failed."));
                continue;
            }

            var saveResult = await SaveTranslatedPostAsync(
                source.Id,
                translated.Plan,
                translated.Response,
                postService,
                cancellationToken);

            results.Add(saveResult);
        }

        return TypedResults.Ok(new AiTranslateBlogResult(results
            .OrderBy(x => x.Culture, StringComparer.OrdinalIgnoreCase)
            .ToList()));
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
                SeriesId = request.SeriesId,
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
            existing.SeriesId = request.SeriesId;
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

    private static Task<IResult> PublishPostTranslationGroup(
        long translationGroupId,
        [FromServices] IPostContentService postService,
        CancellationToken cancellationToken = default)
    {
        return SetPostTranslationGroupPublicationStateAsync(
            translationGroupId,
            ContentPublicationState.Published,
            postService,
            cancellationToken);
    }

    private static Task<IResult> UnpublishPostTranslationGroup(
        long translationGroupId,
        [FromServices] IPostContentService postService,
        CancellationToken cancellationToken = default)
    {
        return SetPostTranslationGroupPublicationStateAsync(
            translationGroupId,
            ContentPublicationState.Draft,
            postService,
            cancellationToken);
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

    private static async Task<IResult> SetPostTranslationGroupPublicationStateAsync(
        long translationGroupId,
        ContentPublicationState state,
        IPostContentService postService,
        CancellationToken cancellationToken)
    {
        var variantsResult = await postService.ListCultureVariantsAsync(translationGroupId, cancellationToken);
        if (variantsResult is Result<IReadOnlyList<PostDocument>, AeroError>.Failure variantsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to load post translations",
                Detail = GetErrorMessage(variantsFailure.Error),
                Status = StatusCodes.Status400BadRequest
            });
        }

        var variants = variantsResult is Result<IReadOnlyList<PostDocument>, AeroError>.Ok ok
            ? ok.Value
            : [];

        if (variants.Count == 0)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "No post translations found",
                Detail = $"No translated posts were found for translation group '{translationGroupId}'.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var items = new List<PublicationBulkItem>();
        foreach (var post in variants)
        {
            post.PublicationState = state;
            post.PublishedOn = state == ContentPublicationState.Published
                ? post.PublishedOn ?? DateTimeOffset.UtcNow
                : null;

            var saveResult = await postService.SaveAsync(post, cancellationToken);
            if (saveResult is Result<PostDocument, AeroError>.Failure saveFailure)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update post publication state",
                    Detail = GetErrorMessage(saveFailure.Error),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (saveResult is Result<PostDocument, AeroError>.Ok saveOk)
            {
                items.Add(new PublicationBulkItem(
                    saveOk.Value.Id,
                    saveOk.Value.Culture,
                    saveOk.Value.Title,
                    saveOk.Value.PublicationState == ContentPublicationState.Published));
            }
        }

        return TypedResults.Ok(new PublicationBulkResult(items.Count, items));
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
            vm.TranslationGroupId,
            vm.SeriesId
        );
    }

    private static BlogDetail MapToBlogDetail(PostDocument document)
        => new(
            document.Id,
            document.Title,
            document.Slug,
            document.Excerpt,
            document.SeoTitle,
            document.SeoDescription,
            document.PublishedOn,
            (int)document.PublicationState,
            document.Content,
            document.TagIds ?? [],
            document.CategoryIds ?? [],
            document.AuthorId,
            document.ImageUrl,
            document.Likes,
            document.CreatedOn,
            document.ModifiedOn,
            document.Culture,
            document.TranslationGroupId,
            document.SeriesId);

    private static IReadOnlySet<string> GetSupportedCultures(SitesModel? site)
    {
        var cultures = site?.SupportedCultures.Count > 0
            ? site.SupportedCultures
            : [site?.DefaultCulture ?? SitesModel.DefaultCultureName];

        return cultures
            .Select(ContentSlugDocument.NormalizeCulture)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<AiTranslatedPostPlan> TranslatePostPlanAsync(
        PostDocument source,
        AiTranslatePostPlan plan,
        string? providerId,
        IAiContentTranslationService translationService,
        CancellationToken cancellationToken)
    {
        var fields = BuildTranslatableFields(source);
        if (fields.Count == 0)
        {
            return AiTranslatedPostPlan.Failed(plan, "The source post does not contain translatable content.");
        }

        var response = await translationService.TranslateAsync(
            new TranslateDocumentRequest(fields, source.Culture, plan.Culture, providerId),
            cancellationToken);

        return response switch
        {
            Result<TranslateDocumentResponse>.Ok ok => AiTranslatedPostPlan.Success(plan, ok.Value),
            Result<TranslateDocumentResponse>.Failure failure => AiTranslatedPostPlan.Failed(plan, GetErrorMessage(failure.Error)),
            _ => AiTranslatedPostPlan.Failed(plan, "Unexpected AI translation result.")
        };
    }

    private static List<TranslateDocumentField> BuildTranslatableFields(PostDocument source)
    {
        var fields = new List<TranslateDocumentField>
        {
            new("title", ContentFieldHint.Title, source.Title),
            new("slug", ContentFieldHint.Slug, source.Slug)
        };

        AddOptionalField(fields, "excerpt", ContentFieldHint.Excerpt, source.Excerpt);
        AddOptionalField(fields, "seoTitle", ContentFieldHint.SeoTitle, source.SeoTitle);
        AddOptionalField(fields, "seoDescription", ContentFieldHint.SeoDescription, source.SeoDescription);

        var markdownBlocks = source.Content
            .OfType<MarkdownBlock>()
            .Where(x => !string.IsNullOrWhiteSpace(x.Content))
            .Select((block, index) => new { Block = block, Index = index })
            .ToList();

        foreach (var item in markdownBlocks)
        {
            fields.Add(new TranslateDocumentField(
                $"markdown.{item.Index}",
                ContentFieldHint.MarkdownContent,
                item.Block.Content));
        }

        return fields;
    }

    private static async Task<AiTranslateBlogCultureResult> SaveTranslatedPostAsync(
        long sourcePostId,
        AiTranslatePostPlan plan,
        TranslateDocumentResponse response,
        IPostContentService postService,
        CancellationToken cancellationToken)
    {
        PostDocument target;
        if (plan.ExistingVariant is null)
        {
            var forkResult = await postService.ForkPostForCultureAsync(sourcePostId, plan.Culture, GetTranslatedSlug(response, plan.Slug), cancellationToken);
            if (forkResult is not Result<PostDocument, AeroError>.Ok forkOk)
            {
                return FailedTranslation(plan.Culture, forkResult is Result<PostDocument, AeroError>.Failure failure
                    ? GetErrorMessage(failure.Error)
                    : "Failed to create translated post.");
            }

            target = forkOk.Value;
        }
        else
        {
            target = plan.ExistingVariant;
            target.Slug = plan.Slug;
            target.PublicationState = ContentPublicationState.Draft;
            target.PublishedOn = null;
        }

        ApplyTranslatedFields(target, response);
        var saveResult = await postService.SaveAsync(target, cancellationToken);

        return saveResult switch
        {
            Result<PostDocument, AeroError>.Ok ok => new AiTranslateBlogCultureResult(
                plan.Culture,
                true,
                MapToBlogDetail(ok.Value),
                response.Warnings,
                null),
            Result<PostDocument, AeroError>.Failure failure => FailedTranslation(plan.Culture, GetErrorMessage(failure.Error)),
            _ => FailedTranslation(plan.Culture, "Failed to save translated post.")
        };
    }

    private static void ApplyTranslatedFields(PostDocument target, TranslateDocumentResponse response)
    {
        target.Title = GetTranslated(response, "title", target.Title);
        target.Excerpt = GetTranslated(response, "excerpt", target.Excerpt);
        target.SeoTitle = GetTranslated(response, "seoTitle", target.SeoTitle);
        target.SeoDescription = GetTranslated(response, "seoDescription", target.SeoDescription);

        var markdownBlocks = target.Content
            .OfType<MarkdownBlock>()
            .Where(x => !string.IsNullOrWhiteSpace(x.Content))
            .Select((block, index) => new { Block = block, Index = index });

        foreach (var item in markdownBlocks)
        {
            item.Block.Content = GetTranslated(response, $"markdown.{item.Index}", item.Block.Content);
        }
    }

    private static string GetTranslatedSlug(TranslateDocumentResponse response, string fallback)
    {
        var translated = GetTranslated(response, "slug", fallback);
        return string.IsNullOrWhiteSpace(translated)
            ? fallback
            : ContentSlugDocument.Normalize(translated);
    }

    private static string GetTranslated(TranslateDocumentResponse response, string key, string? fallback)
        => response.TranslatedFields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback ?? string.Empty;

    private static void AddOptionalField(List<TranslateDocumentField> fields, string key, ContentFieldHint hint, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new TranslateDocumentField(key, hint, value));
        }
    }

    private static AiTranslateBlogCultureResult FailedTranslation(string culture, string error)
        => new(culture, false, null, [], error);

    private static string BuildDefaultLocalizedSlug(string slug, string culture)
    {
        var suffix = culture.ToLowerInvariant();
        var normalized = ContentSlugDocument.Normalize(slug);
        return string.IsNullOrWhiteSpace(normalized)
            ? suffix
            : $"{normalized}-{suffix}";
    }

    private static string GetErrorMessage(AeroError error) => error switch
    {
        AeroError.Error e => e.msg,
        AeroError.NotFound e => e.msg,
        AeroError.Conflict e => e.msg,
        AeroError.Database e => e.msg,
        AeroError.Unauthorized e => e.msg,
        AeroError.Forbidden e => e.msg,
        AeroError.Timeout e => e.msg,
        AeroError.InvalidRequest e => e.msg,
        AeroError.BadRequest e => e.msg,
        AeroError.Exists e => e.msg,
        AeroError.NullReferro e => e.msg,
        AeroError.Cancelled e => e.msg,
        AeroError.NotAllowed e => e.msg,
        AeroError.Configuration e => e.msg,
        AeroError.Validation e => string.Join("; ", e.Errors),
        AeroError.HttpRequest e => e.msg ?? "HTTP request error",
        _ => error.ToString()
    };

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

    private sealed record AiTranslatePostPlan(
        string Culture,
        string Slug,
        PostDocument? ExistingVariant);

    private sealed record AiTranslatedPostPlan(
        AiTranslatePostPlan Plan,
        bool Succeeded,
        TranslateDocumentResponse? Response,
        string? Error)
    {
        public string Culture => Plan.Culture;

        public static AiTranslatedPostPlan Success(AiTranslatePostPlan plan, TranslateDocumentResponse response)
            => new(plan, true, response, null);

        public static AiTranslatedPostPlan Failed(AiTranslatePostPlan plan, string error)
            => new(plan, false, null, error);
    }
}
