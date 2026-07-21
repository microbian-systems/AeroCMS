using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Client.Services;

/// <summary>HTTP strategy for the selected-site Commerce manager API.</summary>
public sealed class CommerceManagerHttpClient(HttpClient httpClient, ILogger<CommerceManagerHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ICommerceManagerClient
{
    public override string Path => "admin/commerce/catalog";

    public Task<Result<ManagerCatalogPage<ManagerProductDto>, AeroError>> GetProductsAsync(string? search, int skip, int take, CancellationToken ct = default)
        => GetAsync<ManagerCatalogPage<ManagerProductDto>>(BuildQuery("products", search, null, skip, take), ct);

    public Task<Result<ManagerProductDto, AeroError>> GetProductAsync(long id, CancellationToken ct = default)
        => GetAsync<ManagerProductDto>($"products/{id}", ct);

    public Task<Result<ManagerProductDto, AeroError>> CreateProductAsync(ManagerProductRequest request, CancellationToken ct = default)
        => PostAsync<ManagerProductRequest, ManagerProductDto>("products", request, ct);

    public Task<Result<ManagerProductDto, AeroError>> UpdateProductAsync(long id, ManagerProductRequest request, CancellationToken ct = default)
        => PutAsync<ManagerProductRequest, ManagerProductDto>($"products/{id}", request, ct);

    public async Task<Result<bool, AeroError>> DeleteProductAsync(long id, CancellationToken ct = default)
        => await MapDeleteAsync(base.DeleteAsync($"products/{id}", ct));

    public Task<Result<ManagerCatalogPage<ManagerListingDto>, AeroError>> GetListingsAsync(string? culture, string? search, int skip, int take, CancellationToken ct = default)
        => GetAsync<ManagerCatalogPage<ManagerListingDto>>(BuildQuery("listings", search, culture, skip, take), ct);

    public Task<Result<ManagerListingDto, AeroError>> GetListingAsync(long id, CancellationToken ct = default)
        => GetAsync<ManagerListingDto>($"listings/{id}", ct);

    public Task<Result<ManagerListingDto, AeroError>> CreateListingAsync(ManagerListingRequest request, CancellationToken ct = default)
        => PostAsync<ManagerListingRequest, ManagerListingDto>("listings", request, ct);

    public Task<Result<ManagerListingDto, AeroError>> UpdateListingAsync(long id, ManagerListingRequest request, CancellationToken ct = default)
        => PutAsync<ManagerListingRequest, ManagerListingDto>($"listings/{id}", request, ct);

    public async Task<Result<bool, AeroError>> DeleteListingAsync(long id, CancellationToken ct = default)
        => await MapDeleteAsync(base.DeleteAsync($"listings/{id}", ct));

    private static string BuildQuery(string resource, string? search, string? culture, int skip, int take)
    {
        var query = $"{resource}?skip={Math.Max(0, skip)}&take={Math.Clamp(take, 1, 100)}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search.Trim())}";
        if (!string.IsNullOrWhiteSpace(culture)) query += $"&culture={Uri.EscapeDataString(culture.Trim())}";
        return query;
    }

    private static async Task<Result<bool, AeroError>> MapDeleteAsync(Task<Result<HttpResponseMessage, AeroError>> operation)
    {
        var result = await operation;
        return result switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure failure => failure.Error,
            _ => AeroError.CreateError("Unexpected Commerce delete result.")
        };
    }
}
