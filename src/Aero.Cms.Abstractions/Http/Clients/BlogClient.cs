namespace Aero.Cms.Abstractions.Http.Clients;

using System.Net.Http.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for blog HTTP client.
/// </summary>
public interface IBlogHttpClient
{
    /// <summary>
    /// Gets all blog posts with pagination and optional search.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of blog summaries or an error.</returns>
    Task<Result<PagedResult<BlogSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Gets blog posts grouped by translation group for manager localization UX.
    /// </summary>
    Task<Result<PagedResult<BlogTranslationGroupSummary>, AeroError>> GetTranslationGroupsAsync(int skip = 0, int take = 10, string? search = null, string? culture = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a blog post by its identifier.
    /// </summary>
    /// <param name="id">The blog post identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<BlogDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default);

    Task<Result<BlogDetail, AeroError>> ForkToCultureAsync(long id, ForkBlogCultureRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a new blog post.
    /// </summary>
    /// <param name="request">The create blog request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> CreateAsync(CreateBlogRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing blog post.
    /// </summary>
    /// <param name="id">The blog post identifier to update.</param>
    /// <param name="request">The update blog request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> UpdateAsync(long id, UpdateBlogRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a blog post.
    /// </summary>
    /// <param name="id">The blog post identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Deletes every localized variant in a post translation group.
    /// </summary>
    Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken ct = default);

    /// <summary>
    /// Publishes a blog post.
    /// </summary>
    /// <param name="id">The blog post identifier to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Unpublishes a blog post.
    /// </summary>
    /// <param name="id">The blog post identifier to unpublish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Imports blog posts from a file (JSON, MD, or ZIP containing JSON files).
    /// </summary>
    /// <param name="request">The file import request with Base64-encoded content and options.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The import result with counts of imported/skipped posts.</returns>
    Task<Result<ImportBlogResult, AeroError>> ImportAsync(ImportFileRequest request, CancellationToken ct = default);
}

/// <summary>
/// Typed client for blog endpoints.
/// </summary>
public class BlogHttpClient(HttpClient httpClient, ILogger<BlogHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IBlogHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/blogs";

    /// <inheritdoc />
    public Task<Result<PagedResult<BlogSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default)
    {
        var url = $"?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<BlogSummary>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<BlogTranslationGroupSummary>, AeroError>> GetTranslationGroupsAsync(int skip = 0, int take = 10, string? search = null, string? culture = null, CancellationToken ct = default)
    {
        var url = $"translation-groups?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(culture)) url += $"&culture={Uri.EscapeDataString(culture)}";
        return GetAsync<PagedResult<BlogTranslationGroupSummary>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<BlogDetail>($"{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<BlogDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<BlogDetail>>($"{id}/translations", ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> ForkToCultureAsync(long id, ForkBlogCultureRequest request, CancellationToken ct = default)
    {
        return PostAsync<ForkBlogCultureRequest, BlogDetail>($"{id}/translations", request, ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> CreateAsync(CreateBlogRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreateBlogRequest, BlogDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> UpdateAsync(long id, UpdateBlogRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdateBlogRequest, BlogDetail>(id.ToString(), request, ct);
    }

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var response = await base.DeleteAsync(id.ToString(), ct);
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from DeleteAsync")
        };
    }

    /// <inheritdoc />
    public async Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken ct = default)
    {
        var response = await base.DeleteAsync($"translation-groups/{translationGroupId}", ct);
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok ok => await ReadDeleteTranslationGroupResultAsync(ok.Value, ct),
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from DeleteTranslationGroupAsync")
        };
    }

    private static async Task<Result<int, AeroError>> ReadDeleteTranslationGroupResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<DeleteBlogTranslationGroupResult>(cancellationToken: ct);
            return body?.Deleted ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, BlogDetail>($"{id}/publish", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, BlogDetail>($"{id}/unpublish", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<ImportBlogResult, AeroError>> ImportAsync(ImportFileRequest request, CancellationToken ct = default)
    {
        return PostAsync<ImportFileRequest, ImportBlogResult>("import", request, ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a blog post.
/// </summary>
/// <param name="Id">The blog post identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="CreatedAt">The creation time.</param>
/// <param name="PublishedAt">The publication time.</param>
/// <param name="Excerpt">The post excerpt.</param>
/// <param name="FeaturedImageUrl">The featured image URL.</param>
public record BlogSummary(long Id, string Title, string Slug, DateTime CreatedAt, DateTime? PublishedAt, string? Excerpt, string? FeaturedImageUrl);

/// <summary>
/// Translation-group summary for the posts manager.
/// </summary>
public sealed record BlogTranslationGroupSummary(
    long TranslationGroupId,
    long DisplayPostId,
    string DisplayCulture,
    string DefaultCulture,
    string Title,
    string Slug,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    string? Excerpt,
    string? FeaturedImageUrl,
    bool MissingDefaultCulture,
    bool MissingSelectedCulture,
    IReadOnlyList<BlogTranslationVariantSummary> Variants);

/// <summary>
/// Culture-specific post variant summary.
/// </summary>
public sealed record BlogTranslationVariantSummary(
    long Id,
    string Culture,
    string Title,
    string Slug,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    bool IsDefaultCulture);

public sealed record DeleteBlogTranslationGroupResult(int Deleted);

/// <summary>
/// Detailed information for a blog post.
/// </summary>
public record BlogDetail(
    long Id,
    string Title,
    string Slug,
    string? Excerpt,
    string? SeoTitle,
    string? SeoDescription,
    DateTimeOffset? PublishedOn,
    int PublicationState,
    List<BlockBase>? Content,
    List<long> TagIds,
    List<long> CategoryIds,
    long? AuthorId,
    string? ImageUrl,
    int Likes,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ModifiedOn,
    string Culture = "en-US",
    long? TranslationGroupId = null);

public sealed record ForkBlogCultureRequest(string Culture, string Slug);

/// <summary>
/// Request to create a new blog post.
/// </summary>
public class CreateBlogRequest
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? MarkdownContent { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
    public string? Author { get; set; }
    public string? ImageUrl { get; set; }
    public int PublicationState { get; set; }
}

/// <summary>
/// Request to update an existing blog post.
/// </summary>
public class UpdateBlogRequest
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? MarkdownContent { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
    public string? Author { get; set; }
    public string? ImageUrl { get; set; }
    public int PublicationState { get; set; }
}

// ─── Import Feature DTOs ─────────────────────────────────────

/// <summary>
/// Behavior when an imported post's slug already exists.
/// </summary>
public static class DuplicateSlugBehavior
{
    public const string Skip = "skip";
    public const string Suffix = "suffix";
    public const string Overwrite = "overwrite";
}

/// <summary>
/// Request to import blog posts from a file (JSON, MD, or ZIP).
/// </summary>
public sealed record ImportFileRequest(
    string FileName,
    string MimeType,
    string Base64Data,
    bool StoreLocalImages,
    string DuplicateBehavior,
    long? DefaultAuthorId,
    bool PublishImported,
    long SiteId
);

/// <summary>
/// Result of a blog post import operation.
/// </summary>
public sealed record ImportBlogResult(
    int TotalProcessed,
    int TotalImported,
    int TotalSkipped,
    IReadOnlyList<ImportedPostSummary> ImportedPosts,
    IReadOnlyList<SkippedPostInfo> SkippedPosts,
    IReadOnlyList<ImportError> Errors
);

/// <summary>
/// Summary of a successfully imported post.
/// </summary>
public sealed record ImportedPostSummary(long Id, string Slug, string Title);

/// <summary>
/// Information about a skipped post during import.
/// </summary>
public sealed record SkippedPostInfo(string Slug, string Reason);

/// <summary>
/// An error encountered during import processing.
/// </summary>
public sealed record ImportError(string Item, string Message);
