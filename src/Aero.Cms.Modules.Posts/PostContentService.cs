using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Core.Entities;
using Aero.Cms.Shared.Localization;
using Aero.Core.Http;
using FlakeId;
using AeroDB.Sable.Pagination;
using System.Globalization;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Posts;

/// <summary>
/// Defines site-aware persistence, routing, publication, and taxonomy operations for blog posts.
/// </summary>
public interface IPostContentService
{
    /// <summary>
    /// Lists posts for the current site and UI culture, including draft and published states.
    /// </summary>
    /// <param name="skip">The number of matching posts to omit.</param>
    /// <param name="take">The maximum number of posts to return.</param>
    /// <param name="search">Optional text matched case-insensitively against title and slug.</param>
    /// <param name="cancellationToken">A token used to cancel the query or cache access.</param>
    /// <returns>A page and total count, or a failure describing an operational error.</returns>
Task<Result<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>> GetAllPostsAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a post by identifier and requires it to belong to the current site.
    /// </summary>
    /// <param name="id">A valid Snowflake post identifier.</param>
    /// <param name="cancellationToken">A token used to cancel persistence or cache access.</param>
    /// <returns>The document on success, or a failure for invalid, missing, or wrong-site identifiers.</returns>
Task<Result<PostDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a published post by route slug for the current site and UI culture.
    /// </summary>
    /// <param name="slug">The route slug, with or without a leading culture segment.</param>
    /// <param name="cancellationToken">A token used to cancel persistence or cache access.</param>
    /// <returns>The published document, or a failure when no eligible reservation and document exist.</returns>
Task<Result<PostDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the most recently published posts for the current site and UI culture.
    /// </summary>
    /// <param name="count">The maximum number of posts to return.</param>
    /// <param name="cancellationToken">A token used to cancel persistence or cache access.</param>
    /// <returns>The ordered published posts, or a failure describing an operational error.</returns>
Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetLatestPostsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all post documents in a translation group for the current site.
    /// </summary>
    /// <param name="TranslationGroupId">The translation-group identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the query.</param>
    /// <returns>The culture-ordered variants, or a failure describing an operational error.</returns>
Task<Result<IReadOnlyList<PostDocument>, AeroError>> ListCultureVariantsAsync(long TranslationGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and persists a draft culture variant of a post in the current site.
    /// </summary>
    /// <param name="sourcePostId">The source post identifier.</param>
    /// <param name="targetCulture">A culture supported by the source post's site.</param>
    /// <param name="targetSlug">The route slug for the new variant.</param>
    /// <param name="cancellationToken">A token used to cancel lookup and persistence work.</param>
    /// <returns>The persisted draft, or a not-found, validation, conflict, or operational failure.</returns>
Task<Result<PostDocument, AeroError>> ForkPostForCultureAsync(long sourcePostId, string targetCulture, string targetSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a post, reserves its culture-specific slug, stamps audit fields, and publishes an update event.
    /// </summary>
    /// <param name="post">The document to persist.</param>
    /// <param name="cancellationToken">A token used to cancel lookup, reservation, persistence, or publication work.</param>
    /// <returns>The persisted document, or a failure describing validation or operational errors.</returns>
    /// <remarks>
    /// A new document with a zero site identifier is assigned to the current site; a nonzero site
    /// identifier is retained. Callers must enforce authorization before passing existing or
    /// explicitly site-owned documents.
    /// </remarks>
Task<Result<PostDocument, AeroError>> SaveAsync(PostDocument post, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a post for an explicitly authorized site.
    /// </summary>
    /// <param name="post">The document to persist.</param>
    /// <param name="authorizedSiteId">The positive site identifier already authorized by the caller.</param>
    /// <param name="cancellationToken">A token used to cancel lookup, validation, persistence, or publication work.</param>
    /// <returns>The persisted document, or a failure describing validation or operational errors.</returns>
Task<Result<PostDocument, AeroError>> SaveAsync(
        PostDocument post,
        long authorizedSiteId,
        CancellationToken cancellationToken = default);

    /// <summary>Changes one current-site post's publication state and commits once.</summary>
Task<Result<PostDocument, AeroError>> SetPublicationStateAsync(
        long id,
        ContentPublicationState state,
        CancellationToken cancellationToken = default);

    /// <summary>Changes all current-site variants in a translation group and commits once.</summary>
Task<Result<IReadOnlyList<PostDocument>, AeroError>> SetTranslationGroupPublicationStateAsync(
        long translationGroupId,
        ContentPublicationState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists published current-culture posts in the current site that contain a tag identifier.
    /// </summary>
    /// <param name="tagId">The tag identifier to match.</param>
    /// <param name="cancellationToken">A token used to cancel the query.</param>
    /// <returns>The posts ordered by publication time descending, or an operational failure.</returns>
Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetByTagAsync(long tagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists published current-culture posts in the current site that contain a category identifier.
    /// </summary>
    /// <param name="categoryId">The category identifier to match.</param>
    /// <param name="cancellationToken">A token used to cancel the query.</param>
    /// <returns>The posts ordered by publication time descending, or an operational failure.</returns>
Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetByCategoryAsync(long categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages published posts for the current site and UI culture after a leading offset.
    /// </summary>
    /// <param name="pageNumber">The page number passed to Sable pagination.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="skip">The number of latest matching posts excluded before paging.</param>
    /// <param name="cancellationToken">A token used to cancel the query.</param>
    /// <returns>The paged result, or a failure describing an operational error.</returns>
Task<Result<IPagedList<PostDocument>, AeroError>> GetPagedPostsAsync(int pageNumber, int pageSize, int skip = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists base tag documents for the current site ordered by name.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the query.</param>
    /// <returns>The site tags, or a failure describing an operational error.</returns>
Task<Result<IReadOnlyList<Tag>, AeroError>> GetAllTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists base category documents for the current site ordered by name.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the query.</param>
    /// <returns>The site categories, or a failure describing an operational error.</returns>
Task<Result<IReadOnlyList<Category>, AeroError>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an author by identifier.
    /// </summary>
    /// <param name="authorId">A valid Snowflake author identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the lookup.</param>
    /// <returns>The author, or a failure when it is invalid, missing, or cannot be loaded.</returns>
    /// <remarks>Author documents are not site-owned, so the current site does not constrain this lookup.</remarks>
Task<Result<PostAuthor?, AeroError>> GetAuthorAsync(long authorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a current-site post and its slug reservation, then publishes an update event.
    /// </summary>
    /// <param name="id">A valid Snowflake post identifier.</param>
    /// <param name="cancellationToken">A token used to cancel persistence or publication work.</param>
    /// <returns><see langword="true"/> after deletion, or a failure for invalid, missing, wrong-site, or operational errors.</returns>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all current-site posts and slug reservations in a translation group.
    /// </summary>
    /// <param name="translationGroupId">The translation-group identifier.</param>
    /// <param name="cancellationToken">A token used to cancel persistence or publication work.</param>
    /// <returns>The number of deleted variants, zero when none exist, or an operational failure.</returns>
Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements site-aware post operations over a caller-supplied Sable session.
/// </summary>
/// <param name="session">The session that owns queued writes and commits.</param>
/// <param name="siteContext">The current site boundary used by scoped queries.</param>
/// <param name="bus">An optional bus for post-commit content-update events.</param>
/// <param name="httpContextAccessor">An optional source for the modifying principal name.</param>
/// <param name="cache">An optional cache used by list and lookup operations.</param>
/// <remarks>
/// Public methods translate thrown exceptions, including cancellation exceptions, into
/// <see cref="AeroError"/> failures. Database commits precede bus publication, so a returned
/// publication failure does not imply that the database mutation was rolled back.
/// </remarks>
public sealed class PostContentService(
    IDocumentSession session,
    ISiteContext siteContext,
    IMessageBus? bus = null,
    IHttpContextAccessor? httpContextAccessor = null,
    IFusionCache? cache = null) : IPostContentService
{
    private const string BlogCacheTag = "blog-index";
    private readonly ISiteContext _siteContext = siteContext;

    /// <inheritdoc />
public Task<Result<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>> GetAllPostsAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default)
        => GetAllPostsAsync(skip, take, search, culture: null, cancellationToken);

    /// <summary>
    /// Lists posts for an explicit normalized culture, including drafts.
    /// </summary>
    /// <param name="skip">The number of matching posts to omit.</param>
    /// <param name="take">The maximum number of posts to return.</param>
    /// <param name="search">Optional title-or-slug search text.</param>
    /// <param name="culture">The requested culture; <see langword="null"/> uses the current UI culture.</param>
    /// <param name="cancellationToken">A token used to cancel the query or cache access.</param>
    /// <returns>A page and total count, or an <see cref="AeroError"/> failure.</returns>
public async Task<Result<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>> GetAllPostsAsync(int skip, int take, string? search, string? culture, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var cacheKey = BuildCacheKey($"list:{currentCulture}:{skip}:{take}:{NormalizeCachePart(search)}");
            var cached = await TryGetCacheAsync<BlogPostListCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>((cached.Items, cached.TotalCount));
            }

            var query = session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.Culture == currentCulture);

            IQueryable<PostDocument> filteredQuery = query;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = query.Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
            }

            var stats = new global::AeroDB.Sable.QueryStatistics();
            var posts = await ((global::AeroDB.Sable.ISableQueryable<PostDocument>)filteredQuery)
                .OrderByDescending(x => x.CreatedOn)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            await SetCacheAsync(cacheKey, new BlogPostListCacheEntry(posts.ToList(), stats.TotalResults), cancellationToken);
            return Prelude.Ok<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>((posts, stats.TotalResults));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(id);
            var post = await session.LoadAsync<PostDocument>(id, cancellationToken);
            if (post is null || post.SiteId != _siteContext.SiteId)
                return Prelude.Fail<bool, AeroError>(AeroError.CreateError($"Blog post with id '{id}' not found or access denied"));

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.BlogPost && x.SiteId == _siteContext.SiteId, cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            session.Delete<PostDocument>(id);
            await session.SaveChangesAsync(cancellationToken);
            await PublishContentUpdatedAsync(post, post.Slug, cancellationToken);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<PostDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(id);
            var cacheKey = BuildCacheKey($"id:{id}");
            var cached = await TryGetCacheAsync<PostDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached.SiteId == _siteContext.SiteId
                    ? Prelude.Ok<PostDocument?, AeroError>(cached)
                    : Prelude.Fail<PostDocument?, AeroError>(
                        AeroError.NotFoundError($"Blog post with id '{id}' not found or access denied"));
            }

            var document = await session.LoadAsync<PostDocument>(id, cancellationToken);
            if (document is null || document.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PostDocument?, AeroError>(AeroError.CreateError($"Blog post with id '{id}' not found or access denied"));
            }

            await SetCacheAsync(cacheKey, document, cancellationToken);
            return Prelude.Ok<PostDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public Task<Result<PostDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => FindBySlugAsync(slug, culture: null, cancellationToken);

    /// <summary>
    /// Resolves a published post for an explicit culture, falling back to the site's default-culture reservation.
    /// </summary>
    /// <param name="slug">The route slug, with or without a leading culture segment.</param>
    /// <param name="culture">The requested culture; <see langword="null"/> uses the current UI culture.</param>
    /// <param name="cancellationToken">A token used to cancel lookup and cache access.</param>
    /// <returns>The published post, or an <see cref="AeroError"/> failure.</returns>
public async Task<Result<PostDocument?, AeroError>> FindBySlugAsync(string slug, string? culture, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var routeSlug = AeroCultureRoute.StripLeadingCulture(slug);
            var cacheKey = BuildCacheKey($"slug:{currentCulture}:{NormalizeCachePart(routeSlug)}");
            var cached = await TryGetCacheAsync<PostDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached.SiteId == _siteContext.SiteId
                    && cached.PublicationState == ContentPublicationState.Published
                    ? Prelude.Ok<PostDocument?, AeroError>(cached)
                    : Prelude.Fail<PostDocument?, AeroError>(
                        AeroError.NotFoundError($"Blog post with slug '{routeSlug}' not found"));
            }

            var normalizedSlug = ContentSlugDocument.Normalize(routeSlug);
            var reservation = await FindSlugReservationAsync(normalizedSlug, currentCulture, cancellationToken)
                ?? await FindDefaultCultureSlugReservationAsync(normalizedSlug, currentCulture, cancellationToken);

            if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.BlogPost)
            {
                return Prelude.Fail<PostDocument?, AeroError>(AeroError.NotFoundError($"Blog post with slug '{routeSlug}' not found"));
            }

            var document = await session.LoadAsync<PostDocument>(reservation.OwnerId, cancellationToken);
            if (document is null || document.SiteId != _siteContext.SiteId)
                return Prelude.Fail<PostDocument?, AeroError>(AeroError.NotFoundError($"Blog post with id '{reservation.OwnerId}' not found"));

            // Filter by published state — unpublished posts must not be publicly accessible
            if (document.PublicationState != ContentPublicationState.Published)
                return Prelude.Fail<PostDocument?, AeroError>(AeroError.NotFoundError($"Blog post with slug '{slug}' not found"));

            await SetCacheAsync(cacheKey, document, cancellationToken);
            return Prelude.Ok<PostDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetLatestPostsAsync(int count, CancellationToken cancellationToken = default)
        => GetLatestPostsAsync(count, culture: null, cancellationToken);

    /// <summary>
    /// Lists the latest published posts for an explicit culture.
    /// </summary>
    /// <param name="count">The maximum number of posts to return.</param>
    /// <param name="culture">The requested culture; <see langword="null"/> uses the current UI culture.</param>
    /// <param name="cancellationToken">A token used to cancel the query or cache access.</param>
    /// <returns>The publication-time ordered posts, or an <see cref="AeroError"/> failure.</returns>
public async Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetLatestPostsAsync(int count, string? culture, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var cacheKey = BuildCacheKey($"latest:{currentCulture}:{count}");
            var cached = await TryGetCacheAsync<BlogPostCollectionCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(cached.Items);
            }

            var latest = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.Culture == currentCulture)
                .Where(x => x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .Take(count)
                .ToListAsync(cancellationToken);

            await SetCacheAsync(cacheKey, new BlogPostCollectionCacheEntry(latest.ToList()), cancellationToken);
            return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(latest);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

/// <inheritdoc />
public Task<Result<PostDocument, AeroError>> SaveAsync(
        PostDocument post,
        CancellationToken cancellationToken = default)
        => SaveAsync(post, _siteContext.SiteId, cancellationToken);

    /// <inheritdoc />
public async Task<Result<PostDocument, AeroError>> SaveAsync(
        PostDocument post,
        long authorizedSiteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(post);
            ValidateId(post.Id);
            if (authorizedSiteId <= 0)
            {
                return Prelude.Fail<PostDocument, AeroError>(
                    AeroError.ValidationError(["The authorized site identifier must be positive."]));
            }

            var existingPost = await session.LoadAsync<PostDocument>(post.Id, cancellationToken);
            if ((post.SiteId != 0 && post.SiteId != authorizedSiteId)
                || (existingPost is not null && existingPost.SiteId != authorizedSiteId))
            {
                return Prelude.Fail<PostDocument, AeroError>(
                    AeroError.NotFoundError($"Blog post with id '{post.Id}' not found or access denied"));
            }

            post.SiteId = authorizedSiteId;
            var relationshipError = await ValidateRelationshipsAsync(post, authorizedSiteId, cancellationToken);
            if (relationshipError is not null)
            {
                return Prelude.Fail<PostDocument, AeroError>(relationshipError);
            }

            post.Culture = ContentSlugDocument.NormalizeCulture(post.Culture);
            post.TranslationGroupId ??= post.Id;
            await ContentSlugReservation.ReserveAsync(
                session,
                post.Id,
                ContentSlugOwnerType.BlogPost,
                post.Slug,
                authorizedSiteId,
                post.Culture,
                existingPost?.Slug,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var existingCreatedAtUtc = existingPost?.CreatedOn;
            post.CreatedOn = existingCreatedAtUtc is null || existingCreatedAtUtc == default ? now : existingCreatedAtUtc.Value;
            post.ModifiedOn = now;
            post.ModifiedBy = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            post.PublishedOn = post.PublicationState == ContentPublicationState.Published
                ? existingPost?.PublishedOn ?? now
                : null;

            session.Store(post);
            await session.SaveChangesAsync(cancellationToken);
            await PublishContentUpdatedAsync(post, existingPost?.Slug, cancellationToken);

            return Prelude.Ok<PostDocument, AeroError>(post);
        }
        catch (ArgumentException ex)
        {
            return Prelude.Fail<PostDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetByTagAsync(long tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.Culture == GetCurrentCulture())
                .Where(x => x.TagIds.Contains(tagId) && x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .ToListAsync(cancellationToken);

            return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(posts);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetByCategoryAsync(long categoryId, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.Culture == GetCurrentCulture())
                .Where(x => x.CategoryIds.Contains(categoryId) && x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .ToListAsync(cancellationToken);

            return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(posts);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public Task<Result<IPagedList<PostDocument>, AeroError>> GetPagedPostsAsync(int pageNumber, int pageSize, int skip = 0, CancellationToken cancellationToken = default)
        => GetPagedPostsAsync(pageNumber, pageSize, skip, culture: null, cancellationToken);

    /// <summary>
    /// Pages published posts for an explicit culture after a leading offset.
    /// </summary>
    /// <param name="pageNumber">The page number passed to Sable pagination.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="skip">The number of latest matching posts excluded before paging.</param>
    /// <param name="culture">The requested culture; <see langword="null"/> uses the current UI culture.</param>
    /// <param name="cancellationToken">A token used to cancel the query.</param>
    /// <returns>The paged result, or an <see cref="AeroError"/> failure.</returns>
public async Task<Result<IPagedList<PostDocument>, AeroError>> GetPagedPostsAsync(int pageNumber, int pageSize, int skip, string? culture, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var query = session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.Culture == currentCulture)
                .Where(x => x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .Skip(skip);

            var pagedList = await AeroDB.Sable.Pagination.PagedListQueryableExtensions
                .ToPagedListAsync(query, pageNumber, pageSize, cancellationToken);

            return Prelude.Ok<IPagedList<PostDocument>, AeroError>(pagedList);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IPagedList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<IReadOnlyList<Tag>, AeroError>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = await session.Query<Tag>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return Prelude.Ok<IReadOnlyList<Tag>, AeroError>(tags);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<Tag>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<IReadOnlyList<Category>, AeroError>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await session.Query<Category>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return Prelude.Ok<IReadOnlyList<Category>, AeroError>(categories);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<Category>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<PostAuthor?, AeroError>> GetAuthorAsync(long authorId, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(authorId);
            var author = await session.LoadAsync<PostAuthor>(authorId, cancellationToken);
            return author is null
                ? Prelude.Fail<PostAuthor?, AeroError>(AeroError.CreateError($"Author with id '{authorId}' not found"))
                : Prelude.Ok<PostAuthor?, AeroError>(author);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostAuthor?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <summary>
    /// Rejects taxonomy identifiers that do not belong to the selected site without disclosing
    /// whether the referenced resource exists elsewhere.
    /// </summary>
    private async Task<AeroError?> ValidateRelationshipsAsync(
        PostDocument post,
        long authorizedSiteId,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var tagIds = post.TagIds.Distinct().ToList();
        var categoryIds = post.CategoryIds.Distinct().ToList();
        post.TagIds = tagIds;
        post.CategoryIds = categoryIds;

        if (post.SeriesId is { } seriesId)
        {
            if (seriesId <= 0)
            {
                errors.Add("Series identifier must be positive.");
            }
            else
            {
                var series = await session.LoadAsync<Series>(seriesId, cancellationToken);
                if (series is null || series.SiteId != authorizedSiteId)
                    errors.Add("The selected series is not valid for the current site.");
            }
        }

        if (tagIds.Any(id => id <= 0))
        {
            errors.Add("Tag identifiers must be positive.");
        }
        else if (tagIds.Count > 0)
        {
            var tags = await session.Query<Tag>()
                .Where(x => x.SiteId == authorizedSiteId && tagIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
            if (tags.Count != tagIds.Count)
                errors.Add("One or more selected tags are not valid for the current site.");
        }

        if (categoryIds.Any(id => id <= 0))
        {
            errors.Add("Category identifiers must be positive.");
        }
        else if (categoryIds.Count > 0)
        {
            var categories = await session.Query<Category>()
                .Where(x => x.SiteId == authorizedSiteId && categoryIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
            if (categories.Count != categoryIds.Count)
                errors.Add("One or more selected categories are not valid for the current site.");
        }

        return errors.Count == 0 ? null : AeroError.ValidationError(errors);
    }

    /// <summary>
    /// Verifies that an identifier can be parsed by the configured Snowflake representation.
    /// </summary>
    private static void ValidateId(long id)
    {
        var snowflake = Id.Parse(id);
    }

    /// <summary>
    /// Prefixes a cache-key suffix with the current site boundary.
    /// </summary>
    private string BuildCacheKey(string suffix)
        => $"cms:posts:{_siteContext.SiteId}:{suffix}";

    /// <summary>
    /// Finds a normalized slug reservation for the current site and exact culture.
    /// </summary>
    private async Task<ContentSlugDocument?> FindSlugReservationAsync(
        string normalizedSlug,
        string culture,
        CancellationToken cancellationToken)
        => await session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == _siteContext.SiteId &&
                x.Culture == culture &&
                string.Equals(normalizedSlug, x.NormalizedSlug, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

    /// <summary>
    /// Falls back to the site's default culture only when the requested culture differs from it.
    /// </summary>
    private async Task<ContentSlugDocument?> FindDefaultCultureSlugReservationAsync(
        string normalizedSlug,
        string culture,
        CancellationToken cancellationToken)
    {
        var defaultCulture = await GetSiteDefaultCultureAsync(cancellationToken);
        if (string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase))
            return null;

        return await FindSlugReservationAsync(normalizedSlug, defaultCulture, cancellationToken);
    }

    /// <inheritdoc />
public async Task<Result<PostDocument, AeroError>> SetPublicationStateAsync(
        long id,
        ContentPublicationState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(id);
            var post = await session.LoadAsync<PostDocument>(id, cancellationToken);
            if (post is null || post.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PostDocument, AeroError>(
                    AeroError.NotFoundError($"Blog post with id '{id}' not found or access denied"));
            }

            var now = DateTimeOffset.UtcNow;
            post.PublicationState = state;
            post.PublishedOn = state == ContentPublicationState.Published
                ? post.PublishedOn ?? now
                : null;
            post.ModifiedOn = now;
            post.ModifiedBy = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";

            session.Store(post);
            await session.SaveChangesAsync(cancellationToken);
            await PublishContentUpdatedAsync(post, post.Slug, cancellationToken);
            return Prelude.Ok<PostDocument, AeroError>(post);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PostDocument>, AeroError>> SetTranslationGroupPublicationStateAsync(
        long translationGroupId,
        ContentPublicationState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(translationGroupId);
            var variants = await session.Query<PostDocument>()
                .Where(x =>
                    x.SiteId == _siteContext.SiteId
                    && x.TranslationGroupId == translationGroupId)
                .ToListAsync(cancellationToken);

            if (variants.Count == 0)
            {
                return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(
                    AeroError.NotFoundError(
                        $"Post translation group '{translationGroupId}' not found or access denied"));
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var post in variants)
            {
                post.PublicationState = state;
                post.PublishedOn = state == ContentPublicationState.Published
                    ? post.PublishedOn ?? now
                    : null;
                post.ModifiedOn = now;
                post.ModifiedBy = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
                session.Store(post);
            }

            await session.SaveChangesAsync(cancellationToken);

            foreach (var post in variants)
            {
                await PublishContentUpdatedAsync(post, post.Slug, cancellationToken);
            }

            return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(variants);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Documents and reservations are committed together. Update events are then published one by
    /// one; a later publication failure is returned after the deletion has already committed.
    /// </remarks>
    public async Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var variants = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.TranslationGroupId == translationGroupId)
                .ToListAsync(cancellationToken);

            if (variants.Count == 0)
            {
                return Prelude.Fail<int, AeroError>(
                    AeroError.NotFoundError(
                        $"Post translation group '{translationGroupId}' not found or access denied"));
            }

            var ids = variants.Select(x => x.Id).ToList();
            var reservations = await session.Query<ContentSlugDocument>()
                .Where(x =>
                    x.SiteId == _siteContext.SiteId
                    && ids.Contains(x.OwnerId)
                    && x.OwnerType == ContentSlugOwnerType.BlogPost)
                .ToListAsync(cancellationToken);

            foreach (var reservation in reservations)
            {
                session.Delete(reservation);
            }

            foreach (var variant in variants)
            {
                session.Delete(variant);
            }

            await session.SaveChangesAsync(cancellationToken);

            foreach (var variant in variants)
            {
                await PublishContentUpdatedAsync(variant, variant.Slug, cancellationToken);
            }

            return Prelude.Ok<int, AeroError>(ids.Count);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<int, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<IReadOnlyList<PostDocument>, AeroError>> ListCultureVariantsAsync(
        long TranslationGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var variants = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.TranslationGroupId == TranslationGroupId)
                .OrderBy(x => x.Culture)
                .ToListAsync(cancellationToken);

            return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(variants);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<PostDocument, AeroError>> ForkPostForCultureAsync(
        long sourcePostId,
        string targetCulture,
        string targetSlug,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await session.LoadAsync<PostDocument>(sourcePostId, cancellationToken);
            if (source is null || source.SiteId != _siteContext.SiteId)
                return Prelude.Fail<PostDocument, AeroError>(AeroError.NotFoundError($"Blog post with id '{sourcePostId}' not found or access denied"));

            var normalizedCulture = ContentSlugDocument.NormalizeCulture(targetCulture);
            var supported = await IsSupportedCultureAsync(source.SiteId, normalizedCulture, cancellationToken);
            if (!supported)
                return Prelude.Fail<PostDocument, AeroError>(AeroError.ValidationError([$"Culture '{normalizedCulture}' is not supported by the current site."]));

            var TranslationGroupId = source.TranslationGroupId ?? source.Id;
            var existingVariant = await session.Query<PostDocument>()
                .FirstOrDefaultAsync(x =>
                    x.SiteId == source.SiteId &&
                    x.TranslationGroupId == TranslationGroupId &&
                    x.Culture == normalizedCulture,
                    cancellationToken);

            if (existingVariant is not null)
                return Prelude.Fail<PostDocument, AeroError>(AeroError.ConflictError($"Blog post already has a '{normalizedCulture}' variant."));

            var fork = PostCultureForker.Fork(source, Snowflake.NewId(), normalizedCulture, targetSlug);

            return await SaveAsync(fork, cancellationToken);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    /// <summary>
    /// Loads and normalizes the current site's default culture, using the CMS default when absent.
    /// </summary>
    private async Task<string> GetSiteDefaultCultureAsync(CancellationToken cancellationToken)
    {
        var site = await session.LoadAsync<SitesModel>(_siteContext.SiteId, cancellationToken);
        var defaultCulture = site?.DefaultCulture ?? SitesModel.DefaultCultureName;

        return ContentSlugDocument.NormalizeCulture(defaultCulture);
    }

    /// <summary>
    /// Determines whether a normalized culture is in a site's configured supported-culture set.
    /// </summary>
    private async Task<bool> IsSupportedCultureAsync(long siteId, string culture, CancellationToken cancellationToken)
    {
        var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken);

        if (site is null)
            return false;

        var supportedCultures = site.SupportedCultures.Count > 0
            ? site.SupportedCultures
            : [site.DefaultCulture ?? SitesModel.DefaultCultureName];

        return supportedCultures
            .Select(ContentSlugDocument.NormalizeCulture)
            .Contains(culture, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes an explicit culture or the ambient UI culture.
    /// </summary>
    private static string GetCurrentCulture(string? culture = null)
        => ContentSlugDocument.NormalizeCulture(culture ?? CultureInfo.CurrentUICulture.Name);

    /// <summary>
    /// Reads an optional cache and returns <see langword="null"/> when caching is disabled or misses.
    /// </summary>
    private async Task<T?> TryGetCacheAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        if (cache is null)
        {
            return null;
        }

        var cached = await cache.TryGetAsync<T>(key, token: cancellationToken);
        return cached.HasValue ? cached.Value : null;
    }

    /// <summary>
    /// Stores a value under the shared blog-index invalidation tag when caching is enabled.
    /// </summary>
    private Task SetCacheAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
        => cache is null
            ? Task.CompletedTask
            : cache.SetAsync(key, value, tags: [BlogCacheTag], token: cancellationToken).AsTask();

    /// <summary>
    /// Publishes a cache-invalidation event when a bus is available.
    /// </summary>
    private Task PublishContentUpdatedAsync(PostDocument post, string? oldSlug, CancellationToken cancellationToken)
        => bus is null
            ? Task.CompletedTask
            : bus.PublishAsync(new BlogPostContentUpdatedEvent(post.Id, post.SiteId, post.Slug, oldSlug)).AsTask();

    /// <summary>
    /// Normalizes optional text for use as one cache-key segment.
    /// </summary>
    private static string NormalizeCachePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().Trim('/').ToLowerInvariant();

    /// <summary>
    /// Stores a cached post page together with the provider-reported total.
    /// </summary>
    private sealed record BlogPostListCacheEntry(List<PostDocument> Items, long TotalCount);

    /// <summary>
    /// Stores a cached post collection.
    /// </summary>
    private sealed record BlogPostCollectionCacheEntry(List<PostDocument> Items);
}
