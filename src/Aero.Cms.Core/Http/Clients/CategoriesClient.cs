namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface ICategoriesHttpClient
{
    Task<Result<string, IReadOnlyList<CategorySummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, CategoryDetail>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<string, CategoryDetail>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<Result<string, CategoryDetail>> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for categories endpoints (stub implementation).
/// </summary>
public class CategoriesHttpClient(HttpClient httpClient, ILogger<CategoriesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ICategoriesHttpClient
{
    protected override string ResourceName => "categories";

    public Task<Result<string, IReadOnlyList<CategorySummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<CategorySummary>>(string.Empty, ct);
    }

    public Task<Result<string, CategoryDetail>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<CategoryDetail>($"details/{id}", ct);
    }

    public Task<Result<string, CategoryDetail>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<CreateCategoryRequest, CategoryDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, CategoryDetail>> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        return PutResultAsync<UpdateCategoryRequest, CategoryDetail>(id.ToString(), request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record CategorySummary(long Id, string Name, string Slug, int ContentCount, long? ParentId);
public record CategoryDetail(long Id, string Name, string Slug, string? Description, int ContentCount, long? ParentId, IReadOnlyList<CategorySummary> Children, DateTime CreatedAt);
public record CreateCategoryRequest(string Name, string Slug, string? Description, long? ParentId);
public record UpdateCategoryRequest(string Name, string Slug, string? Description, long? ParentId);
