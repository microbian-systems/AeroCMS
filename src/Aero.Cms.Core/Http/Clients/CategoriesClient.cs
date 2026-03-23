namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

/// <summary>
/// Typed client for categories endpoints (stub implementation).
/// </summary>
public class CategoriesClient(HttpClient httpClient, ILogger<CategoriesClient> logger)
    : AeroClientBase(httpClient, logger)
{
    protected override string ResourceName => "categories";

    public Task<IReadOnlyList<CategorySummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<CategorySummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<CategorySummary>>(Array.Empty<CategorySummary>());
    }

    public Task<CategoryDetail?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<CategoryDetail>($"details/{id}", ct);
    }

    public Task<CategoryDetail?> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        return PostAsync<CategoryDetail?, CreateCategoryRequest>(string.Empty, request, ct);
    }

    public Task<CategoryDetail?> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        return PutAsync<CategoryDetail?, UpdateCategoryRequest>(id.ToString(), request, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record CategorySummary(long Id, string Name, string Slug, int ContentCount, long? ParentId);
public record CategoryDetail(long Id, string Name, string Slug, string? Description, int ContentCount, long? ParentId, IReadOnlyList<CategorySummary> Children, DateTime CreatedAt);
public record CreateCategoryRequest(string Name, string Slug, string? Description, long? ParentId);
public record UpdateCategoryRequest(string Name, string Slug, string? Description, long? ParentId);
