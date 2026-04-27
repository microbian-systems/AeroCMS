namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for categories HTTP client.
/// </summary>
public interface ICategoriesHttpClient
{
    /// <summary>
    /// Gets all categories.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of category summaries or an error.</returns>
    Task<Result<IReadOnlyList<CategorySummary>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information for a specific category.
    /// </summary>
    /// <param name="id">The category identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The category detail or an error.</returns>
    Task<Result<CategoryDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new category.
    /// </summary>
    /// <param name="request">The create category request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created category detail or an error.</returns>
    Task<Result<CategoryDetail, AeroError>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing category.
    /// </summary>
    /// <param name="id">The category identifier to update.</param>
    /// <param name="request">The update category request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated category detail or an error.</returns>
    Task<Result<CategoryDetail, AeroError>> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a category by its identifier.
    /// </summary>
    /// <param name="id">The category identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for categories endpoints.
/// </summary>
public class CategoriesHttpClient(HttpClient httpClient, ILogger<CategoriesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ICategoriesHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/categories";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CategorySummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<CategorySummary>>(string.Empty, ct);
    }

    /// <inheritdoc />
    public Task<Result<CategoryDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<CategoryDetail>($"details/{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<CategoryDetail, AeroError>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreateCategoryRequest, CategoryDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<CategoryDetail, AeroError>> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdateCategoryRequest, CategoryDetail>(id.ToString(), request, ct);
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
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a category.
/// </summary>
/// <param name="Id">The category identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="ContentCount">The number of items in this category.</param>
/// <param name="ParentId">The optional parent category identifier.</param>
public record CategorySummary(long Id, string Name, string Slug, int ContentCount, long? ParentId);

/// <summary>
/// Detailed information for a category.
/// </summary>
/// <param name="Id">The category identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Description">The optional description.</param>
/// <param name="ContentCount">The number of items in this category.</param>
/// <param name="ParentId">The optional parent category identifier.</param>
/// <param name="Children">The list of child categories.</param>
/// <param name="CreatedAt">The creation time.</param>
public record CategoryDetail(long Id, string Name, string Slug, string? Description, int ContentCount, long? ParentId, IReadOnlyList<CategorySummary> Children, DateTime CreatedAt);

/// <summary>
/// Request to create a new category.
/// </summary>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Description">The optional description.</param>
/// <param name="ParentId">The optional parent category identifier.</param>
public record CreateCategoryRequest(string Name, string Slug, string? Description, long? ParentId);

/// <summary>
/// Request to update an existing category.
/// </summary>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Description">The optional description.</param>
/// <param name="ParentId">The optional parent category identifier.</param>
public record UpdateCategoryRequest(string Name, string Slug, string? Description, long? ParentId);
