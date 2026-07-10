

using Aero.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;

namespace Aero.Cms.Abstractions.Actors;

/// <summary>
/// Defines an interface for IAeroAliasActor.
/// </summary>
public interface IAeroAliasActor : IAeroCmsContentActor<AliasViewModel>
{
    /// <summary>
    /// Get all aliases, optionally filtered by <paramref name="siteId"/>.
    /// Returns the full list (unpaged).
    /// </summary>
    Task<List<AliasViewModel>> GetAllAliasesAsync(
        long? siteId = null,
        CancellationToken ct = default);
}
/// <summary>
/// Defines an interface for IAeroAuthorActor.
/// </summary>
public interface IAeroAuthorActor : IAeroCmsContentActor<AuthorViewModel>;
/// <summary>
/// Defines an interface for IAeroCategoryActor.
/// </summary>
public interface IAeroCategoryActor : IAeroCmsContentActor<CategoryViewModel>
{
    /// <summary>Get all categories (unpaged).</summary>
    Task<List<CategoryViewModel>> GetAllAsync(CancellationToken ct = default);
}
/// <summary>
/// Defines an interface for IAeroSeriesActor.
/// </summary>
public interface IAeroSeriesActor : IAeroCmsContentActor<SeriesViewModel>
{
    /// <summary>Get all series (unpaged).</summary>
    Task<List<SeriesViewModel>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Get or create the default General series for a site.</summary>
    Task<SeriesViewModel> EnsureGeneralAsync(long siteId, CancellationToken ct = default);
}
/// <summary>
/// Defines an interface for IAeroDocsActor.
/// </summary>
public interface IAeroDocsActor : IAeroCmsContentActor<DocViewModel>
{
    /// <summary>Get all docs for a site (unpaged).</summary>
    Task<List<DocViewModel>> GetAllBySiteAsync(long siteId, CancellationToken ct = default);
    /// <summary>Get children of a parent doc within a site.</summary>
    Task<List<DocViewModel>> GetChildrenAsync(long parentId, long siteId, CancellationToken ct = default);
    /// <summary>Get top-level doc categories for a site.</summary>
    Task<List<DocViewModel>> GetTopLevelCategoriesAsync(long siteId, CancellationToken ct = default);
    /// <summary>Save a doc (create or update).</summary>
    Task<AeroRequestResponse<DocViewModel>> SaveAsync(DocViewModel vm, CancellationToken ct = default);
    /// <summary>Get all culture variants for a doc.</summary>
    Task<List<DocViewModel>> ListCultureVariantsAsync(long id, CancellationToken ct = default);
    /// <summary>Create a culture-specific copy of a doc.</summary>
    Task<AeroRequestResponse<DocViewModel>> ForkDocForCultureAsync(long id, string culture, string slug, CancellationToken ct = default);
    /// <summary>Publish a doc.</summary>
    Task<AeroRequestResponse<DocViewModel>> PublishAsync(long id, CancellationToken ct = default);
    /// <summary>Unpublish a doc.</summary>
    Task<AeroRequestResponse<DocViewModel>> UnpublishAsync(long id, CancellationToken ct = default);
    /// <summary>Create a child section inside a docs space.</summary>
    Task<AeroRequestResponse<DocViewModel>> CreateChildSectionAsync(long siteId, long spaceId, long parentId, string title, string? summary, CancellationToken ct = default);
    /// <summary>Move a section inside a docs space.</summary>
    Task<AeroRequestResponse<DocViewModel>> MoveSectionAsync(long siteId, long spaceId, long sectionId, long newParentId, int? order, bool rewriteSlug, CancellationToken ct = default);
    /// <summary>Reorder sibling sections inside a docs space.</summary>
    Task<AeroRequestResponse<DocViewModel>> ReorderSectionsAsync(long siteId, long spaceId, long parentId, IReadOnlyList<long> orderedIds, CancellationToken ct = default);
}
/// <summary>
/// Defines an interface for IAeroMediaActor.
/// </summary>
public interface IAeroMediaActor : IAeroCmsContentActor<MediaViewModel>
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<List<MediaViewModel>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// GetPagedAsync method.
    /// </summary>
Task<(List<MediaViewModel> Items, long TotalCount)> GetPagedAsync(long? parentId, int skip, int take, string? search, CancellationToken ct = default);
        /// <summary>
    /// SaveMediaAsync method.
    /// </summary>
Task<AeroRequestResponse<MediaViewModel>> SaveMediaAsync(MediaViewModel vm, CancellationToken ct = default);
        /// <summary>
    /// DeleteMediaAsync method.
    /// </summary>
Task<AeroRequestResponse<MediaViewModel>> DeleteMediaAsync(long id, CancellationToken ct = default);
}
/// <summary>
/// Defines an interface for IAeroPageActor.
/// </summary>
public interface IAeroPageActor : IAeroCmsContentActor<PageViewModel>
{
    /// <summary>Find a published page by slug for a specific site and culture.</summary>
    Task<AeroRequestResponse<PageViewModel>> GetBySlugAsync(long siteId, string slug, string? culture, CancellationToken ct);
    /// <summary>Get all pages (paged + optional search) for a site.</summary>
    Task<(List<PageViewModel> Items, long TotalCount)> GetAllPagesAsync(long siteId, int skip, int take, string? search, CancellationToken ct);
    /// <summary>Publish a page (event sourcing).</summary>
    Task<AeroRequestResponse<PageViewModel>> PublishAsync(long id, CancellationToken ct);
    /// <summary>Unpublish a page (event sourcing).</summary>
    Task<AeroRequestResponse<PageViewModel>> UnpublishAsync(long id, CancellationToken ct);
    /// <summary>List all culture variants for a page.</summary>
    Task<List<PageViewModel>> ListCultureVariantsAsync(long id, CancellationToken ct);
    /// <summary>Create a draft culture variant for a page.</summary>
    Task<AeroRequestResponse<PageViewModel>> ForkPageForCultureAsync(long id, string culture, string slug, CancellationToken ct);
    /// <summary>Delete multiple pages.</summary>
    Task<int> DeleteMultipleAsync(long[] ids, bool deleteDescendants, CancellationToken ct);
    /// <summary>Get event stream history for a page.</summary>
    Task<List<PageEventItem>> GetEventHistoryAsync(long id, CancellationToken ct);
}
/// <summary>
/// Defines an interface for IAeroPostActor.
/// </summary>
public interface IAeroPostActor : IAeroCmsContentActor<PostViewModel>
{
    /// <summary>
    /// Get all blog posts (paged + optional search) for a specific site.
    /// </summary>
    Task<(List<PostViewModel> Items, long TotalCount)> GetAllPostsAsync(
        long siteId, int skip, int take, string? search, CancellationToken ct);

    /// <summary>
    /// Save (create or update) a blog post, handling slug reservation + cache eviction.
    /// </summary>
    Task<AeroRequestResponse<PostViewModel>> SavePostAsync(PostViewModel vm, long siteId, CancellationToken ct);

    /// <summary>
    /// Delete a blog post by ID, handling slug cleanup + cache eviction.
    /// </summary>
    Task<AeroRequestResponse<PostViewModel>> DeletePostAsync(long id, long siteId, CancellationToken ct);

    /// <summary>
    /// Publish a blog post by ID.
    /// </summary>
    Task<AeroRequestResponse<PostViewModel>> PublishPostAsync(long id, long siteId, CancellationToken ct);

    /// <summary>
    /// Unpublish a blog post by ID (set to Draft).
    /// </summary>
    Task<AeroRequestResponse<PostViewModel>> UnpublishPostAsync(long id, long siteId, CancellationToken ct);

    /// <summary>Load a post by ID within a site (returns null if not found or wrong site).</summary>
    Task<PostViewModel?> LoadAsync(long id, long siteId, CancellationToken ct);

    /// <summary>Find a published post by slug within a site.</summary>
    Task<PostViewModel?> FindBySlugAsync(string slug, long siteId, CancellationToken ct);

    /// <summary>Find a published post by slug within a site and culture.</summary>
    Task<PostViewModel?> FindBySlugAsync(string slug, long siteId, string? culture, CancellationToken ct);

    /// <summary>List all culture variants for a post.</summary>
    Task<List<PostViewModel>> ListCultureVariantsAsync(long id, CancellationToken ct);

    /// <summary>Create a draft culture variant for a post.</summary>
    Task<AeroRequestResponse<PostViewModel>> ForkPostForCultureAsync(long id, string culture, string slug, CancellationToken ct);

    /// <summary>Get latest N published posts.</summary>
    Task<(List<PostViewModel> Items, long TotalCount)> GetLatestPostsAsync(long siteId, int count, CancellationToken ct);

    /// <summary>Get latest N published posts for a culture.</summary>
    Task<(List<PostViewModel> Items, long TotalCount)> GetLatestPostsAsync(long siteId, int count, string? culture, CancellationToken ct);

    /// <summary>Get paged published posts, skipping the first N latest posts.</summary>
    Task<(List<PostViewModel> Items, int TotalCount, int TotalPages, bool HasNext, bool HasPrev)> GetPagedPostsAsync(long siteId, int page, int pageSize, int skipFromLatest, CancellationToken ct);

    /// <summary>Get paged published posts for a culture, skipping the first N latest posts.</summary>
    Task<(List<PostViewModel> Items, int TotalCount, int TotalPages, bool HasNext, bool HasPrev)> GetPagedPostsAsync(long siteId, int page, int pageSize, int skipFromLatest, string? culture, CancellationToken ct);

    /// <summary>Get all tag IDs mapped to their display names.</summary>
    Task<Dictionary<long, string>> GetTagNameMapAsync(long siteId, CancellationToken ct);

    /// <summary>Get a summary of a post author.</summary>
    Task<(string? Name, string? Bio, string? AvatarUrl)?> GetPostAuthorSummaryAsync(long siteId, long authorId, CancellationToken ct);
}
/// <summary>
/// Defines an interface for IAeroSiteActor.
/// </summary>
public interface IAeroSiteActor : IAeroCmsContentActor<SiteViewModel>;
/// <summary>
/// Defines an interface for IAeroTagActor.
/// </summary>
public interface IAeroTagActor : IAeroCmsContentActor<TagViewModel>
{
    /// <summary>Get all tags (unpaged).</summary>
    Task<List<TagViewModel>> GetAllAsync(CancellationToken ct = default);
}
/// <summary>
/// Defines an interface for IAeroContentItemActor.
/// </summary>
public interface IAeroContentItemActor : IAeroCmsContentActor<ContentItemViewModel>
{
    /// <summary>Get content items by type (paged).</summary>
    Task<(List<ContentItemViewModel> Items, long TotalCount)> GetByTypeAsync(
        long siteId, string contentTypeAlias, int skip, int take, CancellationToken ct);
    /// <summary>Save a content item as draft (create or update).</summary>
    Task<AeroRequestResponse<ContentItemViewModel>> SaveDraftAsync(ContentItemViewModel vm, CancellationToken ct = default);
    /// <summary>Publish a content item.</summary>
    Task<AeroRequestResponse<ContentItemViewModel>> PublishAsync(long id, CancellationToken ct);
    /// <summary>Unpublish a content item.</summary>
    Task<AeroRequestResponse<ContentItemViewModel>> UnpublishAsync(long id, CancellationToken ct);
    /// <summary>Delete a content item by id.</summary>
    Task<AeroRequestResponse<ContentItemViewModel>> DeleteAsync(long id, CancellationToken ct = default);
}
/// <summary>
/// Defines an interface for IAeroContentTypeActor.
/// </summary>
public interface IAeroContentTypeActor : IAeroActor
{
    /// <summary>Get all content types for a site.</summary>
    Task<List<ContentTypeViewModel>> GetAllAsync(long siteId, CancellationToken ct = default);
    /// <summary>Get a content type by alias.</summary>
    Task<ContentTypeViewModel?> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default);
    /// <summary>Create a content type definition.</summary>
    Task<AeroRequestResponse<ContentTypeViewModel>> CreateAsync(ContentTypeViewModel vm, CancellationToken ct = default);
    /// <summary>Update a content type definition.</summary>
    Task<AeroRequestResponse<ContentTypeViewModel>> UpdateAsync(ContentTypeViewModel vm, CancellationToken ct = default);
    /// <summary>Delete a content type by alias.</summary>
    Task<bool> DeleteAsync(long siteId, string alias, CancellationToken ct = default);
}
/// <summary>
/// Defines an interface for IAeroSettingActor.
/// </summary>
public interface IAeroSettingActor : IAeroActor
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<List<SettingSummary>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// GetByKeyAsync method.
    /// </summary>
Task<SettingDetail?> GetByKeyAsync(string key, CancellationToken ct = default);
        /// <summary>
    /// GetByCategoryAsync method.
    /// </summary>
Task<List<SettingDetail>> GetByCategoryAsync(string category, CancellationToken ct = default);
        /// <summary>
    /// SetAsync method.
    /// </summary>
Task<SettingDetail> SetAsync(string key, string value, string category = "General", string type = "string", CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<bool> DeleteAsync(string key, CancellationToken ct = default);
        /// <summary>
    /// GetCategoriesAsync method.
    /// </summary>
Task<List<SettingCategory>> GetCategoriesAsync(CancellationToken ct = default);
}



/// <summary>
/// Defines an interface for IAeroCmsContentActor.
/// </summary>
public interface IAeroCmsContentActor<T> :
    IAeroActor,
    ICruddable<T, long>,
    ICanFindBySite<T, long>,
    ICanFindBySlug<T, long>,
    ICanFindBySlug<T, string>,
    IHaveState<T>
    where T : AeroEntityViewModel;


/// <summary>
/// Defines an interface for IAeroCmsContentActor.
/// </summary>
public interface IAeroCmsContentActor<T, TKey> :
    IAeroActor,
    ICruddable<T, TKey>,
    ICanFindBySite<T, TKey>,
    ICanFindBySlug<T, string>,
    IHaveState<T>
    where T : AeroEntityViewModel
    where TKey : IEquatable<TKey>, IComparable<TKey>;

/// <summary>
/// DTO for a single event in a page's version history (returned by IAeroPageActor).
/// </summary>
[GenerateSerializer]
[Alias("PageEventItem")]
public sealed record PageEventItem(
    [property: Id(0)] long Version,
    [property: Id(1)] string EventType,
    [property: Id(2)] DateTimeOffset Timestamp,
    [property: Id(3)] string StreamKey,
    [property: Id(4)] bool IsArchived
);
