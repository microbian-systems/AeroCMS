using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

public sealed record SavePageCustomComponentRequest(
    string Name,
    NeoPageNode Root,
    string? Description = null,
    string Category = "Custom",
    IReadOnlyList<string>? Tags = null);

public sealed record PageCustomComponentDetail(
    long Id,
    string Name,
    string? Description,
    string Category,
    IReadOnlyList<string> Tags,
    NeoPageNode Root,
    IReadOnlyList<string> ReferencedCatalogIds,
    int SchemaVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IPageCustomComponentsHttpClient
{
    Task<Result<IReadOnlyList<PageCustomComponentDetail>, AeroError>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PageCustomComponentDetail, AeroError>> CreateAsync(
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PageCustomComponentDetail, AeroError>> UpdateAsync(
        long id,
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<NeoPageNode, AeroError>> CreateInstanceAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<Result<bool, AeroError>> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class PageCustomComponentsHttpClient(
    HttpClient httpClient,
    ILogger<PageCustomComponentsHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IPageCustomComponentsHttpClient
{
    public override string Path => "admin/pages/custom-components";

    public Task<Result<IReadOnlyList<PageCustomComponentDetail>, AeroError>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<PageCustomComponentDetail>>(string.Empty, cancellationToken);

    public Task<Result<PageCustomComponentDetail, AeroError>> CreateAsync(
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SavePageCustomComponentRequest, PageCustomComponentDetail>(
            string.Empty,
            request,
            cancellationToken);

    public Task<Result<PageCustomComponentDetail, AeroError>> UpdateAsync(
        long id,
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SavePageCustomComponentRequest, PageCustomComponentDetail>(
            id.ToString(),
            request,
            cancellationToken);

    public Task<Result<NeoPageNode, AeroError>> CreateInstanceAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        PostAsync<object, NeoPageNode>($"{id}/instance", new object(), cancellationToken);

    public async Task<Result<bool, AeroError>> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var result = await base.DeleteAsync(id.ToString(), cancellationToken);
        return result switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure failure => failure.Error,
            _ => AeroError.CreateError("Unexpected custom component delete result.")
        };
    }
}
