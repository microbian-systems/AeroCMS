using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;

namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Enums;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for pages HTTP client.
/// </summary>
public interface IPagesHttpClient
{
    /// <summary>
    /// Gets all pages with pagination and optional search.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of page summaries or an error.</returns>
    Task<Result<PagedResult<PageSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a page detail by its identifier.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Gets a page detail by its slug.
    /// </summary>
    /// <param name="slug">The page slug.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Gets published pages with pagination.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of page summaries or an error.</returns>
    Task<Result<PagedResult<PageSummary>, AeroError>> GetPublishedAsync(int skip = 0, int take = 10, CancellationToken ct = default);

    /// <summary>
    /// Creates a new page.
    /// </summary>
    /// <param name="request">The create page request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing page.
    /// </summary>
    /// <param name="id">The page identifier to update.</param>
    /// <param name="request">The update page request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a page.
    /// </summary>
    /// <param name="id">The page identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Publishes a page.
    /// </summary>
    /// <param name="id">The page identifier to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Unpublishes a page.
    /// </summary>
    /// <param name="id">The page identifier to unpublish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for pages endpoints.
/// </summary>
public class PagesHttpClient(HttpClient httpClient, ILogger<PagesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IPagesHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/pages";

    /// <inheritdoc />
    public Task<Result<PagedResult<PageSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 20, string? search = null, CancellationToken ct = default)
    {
        var url = $"?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<PageSummary>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<PageDetail>(id.ToString(), ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return GetAsync<PageDetail>($"slug/{Uri.EscapeDataString(slug)}", ct);
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<PageSummary>, AeroError>> GetPublishedAsync(int skip = 0, int take = 20, CancellationToken ct = default)
    {
        return GetAsync<PagedResult<PageSummary>>($"published?skip={skip}&take={take}", ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreatePageRequest, PageDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdatePageRequest, PageDetail>(id.ToString(), request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync(id.ToString(), ct));
    }

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var response = await task;
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from HTTP operation")
        };
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, PageDetail>($"{id}/publish", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, PageDetail>($"{id}/unpublish", new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a page.
/// </summary>
public record PageSummary(long Id, string Title, string Slug, DateTime CreatedAt, DateTime? PublishedAt, string? Excerpt);

/// <summary>
/// Detailed information for a page.
/// </summary>
public record PageDetail(
    long Id, 
    string Title, 
    string Slug, 
    string? Excerpt, 
    string? SeoTitle, 
    string? SeoDescription,
    DateTime CreatedAt, 
    DateTime UpdatedAt, 
    DateTime? PublishedAt, 
    ContentPublicationState PublicationState,
    int BlockCount,
    bool ShowInNavMenu,
    IReadOnlyList<EditorBlock>? Blocks);

/// <summary>
/// Request to create a new page.
/// </summary>
public record CreatePageRequest(
    string Title, 
    string Slug, 
    string? Summary, 
    string? SeoTitle, 
    string? SeoDescription, 
    ContentPublicationState PublicationState, 
    IReadOnlyList<LayoutRegion>? LayoutRegions = null, 
    bool ShowInNavMenu = false, 
    IReadOnlyList<EditorBlock>? EditorBlocks = null);

/// <summary>
/// Request to update an existing page.
/// </summary>
public record UpdatePageRequest(
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    ContentPublicationState PublicationState,
    IReadOnlyList<LayoutRegion>? LayoutRegions = null,
    bool ShowInNavMenu = false,
    IReadOnlyList<EditorBlock>? EditorBlocks = null);
