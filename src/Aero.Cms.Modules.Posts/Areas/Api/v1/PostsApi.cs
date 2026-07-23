using Aero.Cms.Abstractions.Ai;
using System.Security.Claims;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Audit;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Modules.Posts;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using CreatePostRequest = Aero.Cms.Modules.Posts.Requests.CreatePostRequest;
using UpdatePostRequest = Aero.Cms.Modules.Posts.Requests.UpdatePostRequest;

namespace Aero.Cms.Modules.Posts.Areas.Api.v1;

/// <summary>
/// Maps post administration, publication, import, translation, and preview endpoints.
/// </summary>
/// <remarks>
/// Administrative and preview endpoints require an authenticated principal. Site-specific
/// permission policies are applied in a later hardening phase.
/// </remarks>
public static class PostsApi
{
    /// <summary>
    /// Maps the blog administration and preview endpoints.
    /// </summary>
    /// <param name="app">The route builder to extend.</param>
    public static void MapBlogApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/blogs")
            .WithTags("Admin - Blog")
            .RequireAuthorization();

        group.MapGet("/", ListPosts)
            .WithName("ListPosts")
            .RequireAuthorization("site:read");

        group.MapGet("/translation-groups", ListPostTranslationGroups)
            .WithName("ListPostTranslationGroups")
            .RequireAuthorization("site:read");

        group.MapGet("/{id:long}", GetPostById)
            .WithName("GetPostById")
            .RequireAuthorization("site:read");

        group.MapGet("/{id:long}/translations", ListPostTranslations)
            .WithName("ListPostTranslations")
            .RequireAuthorization("site:read");

        group.MapGet("/slug/{slug}", GetPostBySlug)
            .WithName("GetPostBySlug")
            .RequireAuthorization("site:read");

        group.MapPost("/", CreatePost)
            .WithName("CreatePost")
            .RequireAuthorization("site:create");

        group.MapPost("/{id:long}/translations", ForkPostToCulture)
            .WithName("ForkPostToCulture")
            .RequireAuthorization("site:create");

        group.MapPost("/{id:long}/ai-translate", TranslatePostWithAi)
            .WithName("TranslatePostWithAi")
            .RequireAuthorization("site:update");

        group.MapPut("/{id:long}", UpdatePost)
            .WithName("UpdatePost")
            .RequireAuthorization("site:update");

        group.MapDelete("/{id:long}", DeletePost)
            .WithName("DeletePost")
            .RequireAuthorization("site:delete");

        group.MapDelete("/translation-groups/{translationGroupId:long}", DeletePostTranslationGroup)
            .WithName("DeletePostTranslationGroup")
            .RequireAuthorization("site:delete");

        group.MapPost("/translation-groups/{translationGroupId:long}/publish", PublishPostTranslationGroup)
            .WithName("PublishPostTranslationGroup")
            .RequireAuthorization("site:update");

        group.MapPost("/translation-groups/{translationGroupId:long}/unpublish", UnpublishPostTranslationGroup)
            .WithName("UnpublishPostTranslationGroup")
            .RequireAuthorization("site:update");

        group.MapPost("/{id:long}/publish", PublishPost)
            .WithName("PublishPost")
            .RequireAuthorization("site:update");

        group.MapPost("/{id:long}/unpublish", UnpublishPost)
            .WithName("UnpublishPost")
            .RequireAuthorization("site:update");

        group.MapPost("/import", ImportPosts)
            .WithName("ImportPosts")
            .RequireAuthorization("site:create");

        // Preview endpoints (moved from Headless PreviewApi)
        app.MapGet($"/{HttpConstants.ApiPrefix}admin/preview/blog-posts/{{id:long}}", PreviewBlogPost)
            .WithName("PreviewBlogPost")
            .WithTags("Admin - Preview")
            .RequireAuthorization("site:read");

        app.MapPost($"/{HttpConstants.ApiPrefix}admin/preview/blog-posts/render-fragment", PreviewBlogPostFragment)
            .WithName("PreviewBlogPostFragment")
            .WithTags("Admin - Preview")
            .RequireAuthorization("site:read");
    }

    /// <summary>
    /// Lists a current-site page and projects actor models to summary contracts.
    /// </summary>
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

    /// <summary>
    /// Loads all current-site variants, groups them in memory, and pages translation-group summaries.
    /// </summary>
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

    /// <summary>
    /// Loads a post by identifier and projects it to the HTTP detail contract.
    /// </summary>
    /// <remarks>The actor lookup is not supplied with the current site identifier.</remarks>
    private static async Task<IResult> GetPostById(
        long id,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var result = await postsActor.GetByIdAsync(id, siteContext.SiteId, cancellationToken);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(MapToBlogDetail(result.data));
    }

    /// <summary>
    /// Lists the culture variants associated with a source identifier.
    /// </summary>
    /// <remarks>The actor derives the site from the persisted source rather than from the request context.</remarks>
    private static async Task<IResult> ListPostTranslations(
        long id,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var source = await postsActor.GetByIdAsync(id, siteContext.SiteId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(source.error.Message))
            return TypedResults.NotFound();

        var variants = await postsActor.ListCultureVariantsAsync(id, siteContext.SiteId, cancellationToken);
        return TypedResults.Ok(variants.Select(MapToBlogDetail).ToList());
    }

    /// <summary>
    /// Persists a draft culture fork through the post actor.
    /// </summary>
    /// <remarks>The actor derives the site from the persisted source rather than from the request context.</remarks>
    private static async Task<IResult> ForkPostToCulture(
        long id,
        [FromBody] ForkBlogCultureRequest request,
        [FromServices] IAeroPostActor postsActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var source = await postsActor.GetByIdAsync(id, siteContext.SiteId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(source.error.Message))
            return TypedResults.NotFound();

        var result = await postsActor.ForkPostForCultureAsync(
            id,
            siteContext.SiteId,
            request.Culture,
            request.Slug,
            cancellationToken);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(new { error = result.error })
            : TypedResults.Ok(MapToBlogDetail(result.data));
    }

    /// <summary>
    /// Translates selected fields concurrently, then saves successful culture plans sequentially as drafts.
    /// </summary>
    /// <remarks>
    /// Duplicate and unsupported cultures are reported per target. Saves are not wrapped in a
    /// cross-culture transaction, so earlier targets remain persisted if a later target fails.
    /// </remarks>
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

    /// <summary>
    /// Finds a published current-site post by slug.
    /// </summary>
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

    /// <summary>
    /// Saves a new current-site post and records an audit event after persistence.
    /// </summary>
    /// <remarks>
    /// The preliminary actor slug lookup only finds published posts; the authoritative slug
    /// reservation occurs during save. Audit logging follows persistence and is not atomic with it.
    /// </remarks>
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
                MarkdownContent = request.MarkdownContent ?? string.Empty,
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

    /// <summary>
    /// Applies editable fields, saves the post under the current site, and records an audit event.
    /// </summary>
    /// <remarks>
    /// The initial identifier lookup is not site-scoped. Persistence and audit logging are separate
    /// operations, and a logging failure can be returned after the post has been saved.
    /// </remarks>
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
            if (request.Id != id)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Post identifier mismatch",
                    Detail = "The request body post identifier must match the route identifier.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var siteId = siteContext.SiteId;

            // Load existing post
            var loadResult = await postsActor.GetByIdAsync(id, siteId, cancellationToken);
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
                existing.MarkdownContent = request.MarkdownContent;
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

    /// <summary>
    /// Deletes a current-site post and records an audit event after persistence.
    /// </summary>
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
            var loadResult = await postsActor.GetByIdAsync(id, siteId, cancellationToken);
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

    /// <summary>
    /// Deletes the current site's documents and reservations for a translation group.
    /// </summary>
    private static async Task<IResult> DeletePostTranslationGroup(
        long translationGroupId,
        [FromServices] IPostContentService postService,
        CancellationToken cancellationToken = default)
    {
        var result = await postService.DeleteTranslationGroupAsync(translationGroupId, cancellationToken);
        return result switch
        {
            Result<int, AeroError>.Failure { Error: AeroError.NotFound } => TypedResults.NotFound(new ProblemDetails
            {
                Title = "Post translation group not found",
                Detail = $"Post translation group '{translationGroupId}' was not found.",
                Status = StatusCodes.Status404NotFound
            }),
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

    /// <summary>
    /// Publishes each variant in a translation group.
    /// </summary>
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

    /// <summary>
    /// Returns each variant in a translation group to draft state.
    /// </summary>
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

    /// <summary>
    /// Publishes one current-site post and logs an update audit event.
    /// </summary>
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
            var existing = await postsActor.GetByIdAsync(id, siteId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(existing.error.Message))
                return TypedResults.NotFound();

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

    /// <summary>
    /// Returns one current-site post to draft state and logs an update audit event.
    /// </summary>
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
            var existing = await postsActor.GetByIdAsync(id, siteId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(existing.error.Message))
                return TypedResults.NotFound();

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

    /// <summary>
    /// Saves variants sequentially with a common publication state.
    /// </summary>
    /// <remarks>There is no group transaction or rollback; processing stops on the first failed save.</remarks>
    private static async Task<IResult> SetPostTranslationGroupPublicationStateAsync(
        long translationGroupId,
        ContentPublicationState state,
        IPostContentService postService,
        CancellationToken cancellationToken)
    {
        var result = await postService.SetTranslationGroupPublicationStateAsync(
            translationGroupId,
            state,
            cancellationToken);

        if (result is Result<IReadOnlyList<PostDocument>, AeroError>.Failure
            {
                Error: AeroError.NotFound
            })
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "No post translations found",
                Detail = $"No translated posts were found for translation group '{translationGroupId}'.",
                Status = StatusCodes.Status404NotFound
            });
        }

        if (result is Result<IReadOnlyList<PostDocument>, AeroError>.Failure)
        {
            return TypedResults.Problem(
                title: "Unable to update post translations",
                detail: "The post translation publication state could not be updated.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var ok = (Result<IReadOnlyList<PostDocument>, AeroError>.Ok)result;
        var items = ok.Value
            .Select(post => new PublicationBulkItem(
                post.Id,
                post.Culture,
                post.Title,
                post.PublicationState == ContentPublicationState.Published))
            .ToList();

        return TypedResults.Ok(new PublicationBulkResult(items.Count, items));
    }

    /// <summary>
    /// Invokes the import pipeline and translates its railway result to an HTTP response.
    /// </summary>
    private static async Task<IResult> ImportPosts(
        [FromBody] ImportFileRequest request,
        [FromServices] IPostImportService importService,
        [FromServices] ISiteContext siteContext,
        [FromServices] IAuthorizationService authorizationService,
        HttpContext httpContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PostsApi));
        try
        {
            if (string.Equals(
                    request.DuplicateBehavior,
                    DuplicateSlugBehavior.Overwrite,
                    StringComparison.OrdinalIgnoreCase))
            {
                var updateAuthorization = await authorizationService.AuthorizeAsync(
                    httpContext.User,
                    policyName: "site:update");
                if (!updateAuthorization.Succeeded)
                    return TypedResults.Forbid();
            }

            var result = await importService.ImportAsync(
                request,
                siteContext.SiteId,
                cancellationToken);

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

    /// <summary>
    /// Parses the current principal's name-identifier claim as a numeric audit identifier.
    /// </summary>
    /// <returns>The parsed identifier, or zero when the claim is absent or invalid.</returns>
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

    /// <summary>
    /// Returns an identifier-based post preview without a publication-state or site filter.
    /// </summary>
    private static async Task<IResult> PreviewBlogPost(
        long id,
        IAeroPostActor postsActor,
        ISiteContext siteContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PostsApi));
        try
        {
            var result = await postsActor.GetByIdAsync(id, siteContext.SiteId, ct);
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

    /// <summary>
    /// Converts supplied Markdown to an HTML preview fragment.
    /// </summary>
    /// <remarks>The Markdig output is returned without an HTML sanitization pass.</remarks>
    private static IResult PreviewBlogPostFragment(
        [FromBody] PreviewBlogPostFragmentRequest request,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PostsApi));
        try
        {
            if (request.MarkdownContent is null)
                return TypedResults.BadRequest(new { error = "Blog post content is required." });

            ct.ThrowIfCancellationRequested();
            var html = Markdown.ToHtml(request.MarkdownContent, PostMarkdownPipelines.Preview);
            return TypedResults.Ok(new PreviewBlogPostFragmentResponse(html));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rendering preview blog post fragment");
            return TypedResults.Json(new { error = "An error occurred rendering the preview fragment." }, statusCode: 500);
        }
    }

    // ── Mapping helpers ────────────────────────────────────────────────

    /// <summary>
    /// Maps <see cref="PostViewModel"/> to the HTTP detail contract.
    /// </summary>
    private static BlogDetail MapToBlogDetail(PostViewModel vm)
    {
        return new BlogDetail(
            vm.Id,
            vm.Title ?? string.Empty,
            vm.Slug ?? string.Empty,
            vm.Excerpt,
            vm.SeoTitle,
            vm.SeoDescription,
            vm.PublishedOn,
            (int)vm.PublicationState,
            vm.MarkdownContent,
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

    /// <summary>
    /// Maps a persistence document to the HTTP detail contract.
    /// </summary>
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
            document.MarkdownContent,
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

    /// <summary>
    /// Normalizes a site's supported cultures, falling back to its default culture.
    /// </summary>
    private static IReadOnlySet<string> GetSupportedCultures(SitesModel? site)
    {
        var cultures = site?.SupportedCultures.Count > 0
            ? site.SupportedCultures
            : [site?.DefaultCulture ?? SitesModel.DefaultCultureName];

        return cultures
            .Select(ContentSlugDocument.NormalizeCulture)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sends one target-culture field set to the configured AI translation service.
    /// </summary>
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

    /// <summary>
    /// Builds the nonblank title, slug, metadata, and Markdown fields sent to the translator.
    /// </summary>
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

        if (!string.IsNullOrWhiteSpace(source.MarkdownContent))
        {
            fields.Add(new TranslateDocumentField(
                "markdown",
                ContentFieldHint.MarkdownContent,
                source.MarkdownContent));
        }

        return fields;
    }

    /// <summary>
    /// Creates or reuses a target variant, applies translated fields, and persists it as a draft.
    /// </summary>
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

    /// <summary>
    /// Applies translated non-slug fields, retaining each target value when a translation is absent.
    /// </summary>
    private static void ApplyTranslatedFields(PostDocument target, TranslateDocumentResponse response)
    {
        target.Title = GetTranslated(response, "title", target.Title);
        target.Excerpt = GetTranslated(response, "excerpt", target.Excerpt);
        target.SeoTitle = GetTranslated(response, "seoTitle", target.SeoTitle);
        target.SeoDescription = GetTranslated(response, "seoDescription", target.SeoDescription);

        target.MarkdownContent = GetTranslated(response, "markdown", target.MarkdownContent);
    }

    /// <summary>
    /// Gets and normalizes the translated slug, or returns the planned fallback.
    /// </summary>
    private static string GetTranslatedSlug(TranslateDocumentResponse response, string fallback)
    {
        var translated = GetTranslated(response, "slug", fallback);
        return string.IsNullOrWhiteSpace(translated)
            ? fallback
            : ContentSlugDocument.Normalize(translated);
    }

    /// <summary>
    /// Gets a nonblank translated field or its non-null fallback.
    /// </summary>
    private static string GetTranslated(TranslateDocumentResponse response, string key, string? fallback)
        => response.TranslatedFields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback ?? string.Empty;

    /// <summary>
    /// Adds a translation field only when its source value is nonblank.
    /// </summary>
    private static void AddOptionalField(List<TranslateDocumentField> fields, string key, ContentFieldHint hint, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new TranslateDocumentField(key, hint, value));
        }
    }

    /// <summary>
    /// Creates a failed per-culture translation result.
    /// </summary>
    private static AiTranslateBlogCultureResult FailedTranslation(string culture, string error)
        => new(culture, false, null, [], error);

    /// <summary>
    /// Appends a lowercase culture suffix to a normalized source slug.
    /// </summary>
    private static string BuildDefaultLocalizedSlug(string slug, string culture)
    {
        var suffix = culture.ToLowerInvariant();
        var normalized = ContentSlugDocument.Normalize(slug);
        return string.IsNullOrWhiteSpace(normalized)
            ? suffix
            : $"{normalized}-{suffix}";
    }

    /// <summary>
    /// Extracts a human-readable message from an <see cref="AeroError"/>.
    /// </summary>
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

    /// <summary>
    /// Selects a display variant and projects a translation group with default-culture flags.
    /// </summary>
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

    /// <summary>
    /// Compares normalized culture names without case sensitivity.
    /// </summary>
    private static bool CultureEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Captures one validated translation target and an optional existing variant.
    /// </summary>
    private sealed record AiTranslatePostPlan(
        string Culture,
        string Slug,
        PostDocument? ExistingVariant);

    /// <summary>
    /// Captures the translation-service outcome before persistence.
    /// </summary>
    private sealed record AiTranslatedPostPlan(
        AiTranslatePostPlan Plan,
        bool Succeeded,
        TranslateDocumentResponse? Response,
        string? Error)
    {
        /// <summary>
        /// Gets the target culture.
        /// </summary>
public string Culture => Plan.Culture;

        /// <summary>
        /// Creates a successful translated plan.
        /// </summary>
public static AiTranslatedPostPlan Success(AiTranslatePostPlan plan, TranslateDocumentResponse response)
            => new(plan, true, response, null);

        /// <summary>
        /// Creates a failed translated plan.
        /// </summary>
public static AiTranslatedPostPlan Failed(AiTranslatePostPlan plan, string error)
            => new(plan, false, null, error);
    }
}
