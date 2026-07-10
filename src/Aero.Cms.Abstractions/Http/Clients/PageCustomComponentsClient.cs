using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>
/// Represents a record for SavePageCustomComponentRequest.
/// </summary>
public sealed record SavePageCustomComponentRequest(
    string Name,
    NeoPageNode Root,
    string? Description = null,
    string Category = "Custom",
    IReadOnlyList<string>? Tags = null);

/// <summary>
/// Represents a record for PageCustomComponentDetail.
/// </summary>
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

/// <summary>
/// Defines an interface for IPageCustomComponentsHttpClient.
/// </summary>
public interface IPageCustomComponentsHttpClient
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<PageCustomComponentDetail>, AeroError>> GetAllAsync(
        CancellationToken cancellationToken = default);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<PageCustomComponentDetail, AeroError>> CreateAsync(
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<PageCustomComponentDetail, AeroError>> UpdateAsync(
        long id,
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// CreateInstanceAsync method.
    /// </summary>
Task<Result<NeoPageNode, AeroError>> CreateInstanceAsync(
        long id,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for PageCustomComponentsHttpClient.
/// </summary>
public sealed class PageCustomComponentsHttpClient(
    HttpClient httpClient,
    ILogger<PageCustomComponentsHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IPageCustomComponentsHttpClient
{
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public override string Path => "admin/pages/custom-components";

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<PageCustomComponentDetail>, AeroError>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<PageCustomComponentDetail>>(string.Empty, cancellationToken);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public Task<Result<PageCustomComponentDetail, AeroError>> CreateAsync(
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SavePageCustomComponentRequest, PageCustomComponentDetail>(
            string.Empty,
            request,
            cancellationToken);

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public Task<Result<PageCustomComponentDetail, AeroError>> UpdateAsync(
        long id,
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SavePageCustomComponentRequest, PageCustomComponentDetail>(
            id.ToString(),
            request,
            cancellationToken);

        /// <summary>
    /// CreateInstanceAsync method.
    /// </summary>
public Task<Result<NeoPageNode, AeroError>> CreateInstanceAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        PostAsync<object, NeoPageNode>($"{id}/instance", new object(), cancellationToken);

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
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
