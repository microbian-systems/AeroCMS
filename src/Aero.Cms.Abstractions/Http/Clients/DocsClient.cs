namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Enums;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for documentation HTTP client.
/// </summary>
public interface IDocsHttpClient
{
    /// <summary>
    /// Gets all documentation summaries.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of documentation summaries or an error.</returns>
    Task<Result<IReadOnlyList<DocsSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a documentation detail by its identifier.
    /// </summary>
    /// <param name="id">The documentation identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The documentation detail or an error.</returns>
    Task<Result<DocsDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Gets a documentation detail by its slug.
    /// </summary>
    /// <param name="slug">The documentation slug.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The documentation detail or an error.</returns>
    Task<Result<DocsDetail, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Gets documentation categories.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of documentation categories or an error.</returns>
    Task<Result<IReadOnlyList<DocsSummary>, AeroError>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets children documentation for a parent.
    /// </summary>
    /// <param name="parentId">The parent identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of children documentation summaries or an error.</returns>
    Task<Result<IReadOnlyList<DocsSummary>, AeroError>> GetChildrenAsync(long parentId, CancellationToken ct = default);

    /// <summary>
    /// Saves a documentation article.
    /// </summary>
    /// <param name="page">The documentation detail to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The saved documentation detail or an error.</returns>
    Task<Result<DocsDetail, AeroError>> SaveAsync(DocsDetail page, CancellationToken ct = default);

    /// <summary>
    /// Creates a child section inside a docs space.
    /// </summary>
    Task<Result<DocsDetail, AeroError>> CreateChildAsync(long spaceId, long parentId, DocsCreateChildRequest request, CancellationToken ct = default);

    /// <summary>
    /// Moves a section inside a docs space.
    /// </summary>
    Task<Result<DocsDetail, AeroError>> MoveAsync(long spaceId, long id, DocsMoveRequest request, CancellationToken ct = default);

    /// <summary>
    /// Reorders sibling sections inside a docs space.
    /// </summary>
    Task<Result<bool, AeroError>> ReorderAsync(long spaceId, DocsReorderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Publishes a documentation article.
    /// </summary>
    /// <param name="id">The documentation identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The published documentation detail or an error.</returns>
    Task<Result<DocsDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Unpublishes a documentation article.
    /// </summary>
    /// <param name="id">The documentation identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The unpublished documentation detail or an error.</returns>
    Task<Result<DocsDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Deletes a documentation article.
    /// </summary>
    /// <param name="id">The documentation identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for documentation endpoints.
/// </summary>
public sealed class DocsHttpClient(HttpClient httpClient, ILogger<DocsHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IDocsHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/docs";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<DocsSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<DocsSummary>>("", ct);

    /// <inheritdoc />
    public Task<Result<DocsDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<DocsDetail>($"{id}", ct);

    /// <inheritdoc />
    public Task<Result<DocsDetail, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default)
        => GetAsync<DocsDetail>($"by-slug/{slug}", ct);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<DocsSummary>, AeroError>> GetCategoriesAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<DocsSummary>>("categories", ct);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<DocsSummary>, AeroError>> GetChildrenAsync(long parentId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<DocsSummary>>($"{parentId}/children", ct);

    /// <inheritdoc />
    public Task<Result<DocsDetail, AeroError>> SaveAsync(DocsDetail page, CancellationToken ct = default)
        => PostAsync<DocsDetail, DocsDetail>("", page, ct);

    /// <inheritdoc />
    public Task<Result<DocsDetail, AeroError>> CreateChildAsync(long spaceId, long parentId, DocsCreateChildRequest request, CancellationToken ct = default)
        => PostAsync<DocsCreateChildRequest, DocsDetail>($"{spaceId}/sections/{parentId}/children", request, ct);

    /// <inheritdoc />
    public Task<Result<DocsDetail, AeroError>> MoveAsync(long spaceId, long id, DocsMoveRequest request, CancellationToken ct = default)
        => PostAsync<DocsMoveRequest, DocsDetail>($"{spaceId}/sections/{id}/move", request, ct);

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> ReorderAsync(long spaceId, DocsReorderRequest request, CancellationToken ct = default)
        => MapBoolResult(PostAsync<DocsReorderRequest>($"{spaceId}/sections/reorder", request, ct));

    /// <inheritdoc />
    public Task<Result<DocsDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default)
        => PostAsync<object, DocsDetail>($"{id}/publish", new { }, ct);

    /// <inheritdoc />
    public Task<Result<DocsDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default)
        => PostAsync<object, DocsDetail>($"{id}/unpublish", new { }, ct);

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync($"{id}", ct));

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
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for documentation articles.
/// </summary>
public record DocsSummary(
    long Id,
    string Title,
    string Slug,
    long? ParentId,
    int Order,
    string? Summary = null,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    DateTimeOffset? PublishedOn = null,
    DateTimeOffset? ModifiedOn = null,
    string? SeoTitle = null,
    string? SeoDescription = null,
    bool ShowHeaderNavigation = true,
    string? HeaderImageUrl = null,
    long PublishedVersion = 0,
    long DraftVersion = 0);

/// <summary>
/// Request to create a child docs section under a parent in a space.
/// </summary>
public sealed record DocsCreateChildRequest(string Title, string? Summary = null);

/// <summary>
/// Request to move a docs section to a new parent.
/// </summary>
public sealed record DocsMoveRequest(long NewParentId, int? Order = null, bool RewriteSlug = true);

/// <summary>
/// Request to save sibling order under a parent.
/// </summary>
public sealed record DocsReorderRequest(long ParentId, IReadOnlyList<long> OrderedIds);

/// <summary>
/// Detailed information for documentation articles.
/// </summary>
public record DocsDetail(
    long Id, 
    string Title, 
    string Slug, 
    string? Summary, 
    string? MarkdownContent, 
    long? ParentId, 
    int Order,
    ContentPublicationState PublicationState,
    string? SeoTitle = null,
    string? SeoDescription = null,
    DateTimeOffset? PublishedOn = null,
    bool ShowHeaderNavigation = true,
    string? HeaderImageUrl = null,
    DateTimeOffset CreatedOn = default,
    DateTimeOffset? ModifiedOn = null,
    long PublishedVersion = 0,
    long DraftVersion = 0)
{
    public static DocsDetail Create(
        string title,
        string slug,
        long? parentId,
        string? summary,
        ContentPublicationState publicationState)
        => new(
            0,
            title.Trim(),
            slug.Trim().Trim('/'),
            summary,
            string.Empty,
            parentId,
            0,
            publicationState);
}
