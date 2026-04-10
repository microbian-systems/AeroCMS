namespace Aero.Cms.Core.Http.Clients;

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
    protected override string ResourceName => "docs";

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
public record DocsSummary(long Id, string Title, string Slug, long? ParentId, int Order);

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
    ContentPublicationState PublicationState);
