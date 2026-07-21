using Aero.Cms.Modules.Commerce.Client.Services;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Commerce.Client.Models;

/// <summary>Searchable product picker that preserves the selected product beyond any result page.</summary>
public sealed class ListingProductPicker(ICommerceManagerClient client)
{
    private const int SearchPageSize = 50;

    public IReadOnlyList<ManagerProductDto> Products { get; private set; } = [];

    public async Task<Result<IReadOnlyList<ManagerProductDto>, AeroError>> SearchAsync(
        string? search,
        long? preserveProductId,
        CancellationToken ct = default)
    {
        var pageResult = await client.GetProductsAsync(search, 0, SearchPageSize, ct);
        if (pageResult is Result<ManagerCatalogPage<ManagerProductDto>, AeroError>.Failure failure)
            return failure.Error;

        var rows = ((Result<ManagerCatalogPage<ManagerProductDto>, AeroError>.Ok)pageResult).Value.Items.ToList();
        if (preserveProductId is > 0 && rows.All(product => product.Id != preserveProductId.Value))
        {
            var selectedResult = await client.GetProductAsync(preserveProductId.Value, ct);
            if (selectedResult is Result<ManagerProductDto, AeroError>.Ok selected)
                rows.Insert(0, selected.Value);
            else if (selectedResult is Result<ManagerProductDto, AeroError>.Failure selectedFailure)
                return selectedFailure.Error;
        }

        Products = rows.DistinctBy(product => product.Id).ToList();
        return new Result<IReadOnlyList<ManagerProductDto>, AeroError>.Ok(Products);
    }
}
