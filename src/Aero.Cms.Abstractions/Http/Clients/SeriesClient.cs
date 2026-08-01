namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Defines an interface for ISeriesHttpClient.
/// </summary>
public interface ISeriesHttpClient
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<SeriesSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<Result<SeriesDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<SeriesDetail, AeroError>> CreateAsync(CreateSeriesRequest request, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<SeriesDetail, AeroError>> UpdateAsync(long id, UpdateSeriesRequest request, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// EnsureGeneralAsync method.
    /// </summary>
Task<Result<SeriesDetail, AeroError>> EnsureGeneralAsync(CancellationToken ct = default);
        /// <summary>
    /// ListTranslationsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<SeriesTranslationSummary>, AeroError>> ListTranslationsAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// UpsertTranslationAsync method.
    /// </summary>
Task<Result<SeriesTranslationSummary, AeroError>> UpsertTranslationAsync(long id, string culture, UpsertSeriesTranslationRequest request, CancellationToken ct = default);
}

/// <summary>
/// Represents a class for SeriesHttpClient.
/// </summary>
public sealed class SeriesHttpClient(HttpClient httpClient, ILogger<SeriesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ISeriesHttpClient
{
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public override string Path => "admin/series";

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<SeriesSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<SeriesSummary>>(string.Empty, ct);

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public Task<Result<SeriesDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<SeriesDetail>($"details/{id}", ct);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public Task<Result<SeriesDetail, AeroError>> CreateAsync(CreateSeriesRequest request, CancellationToken ct = default)
        => PostAsync<CreateSeriesRequest, SeriesDetail>(string.Empty, request, ct);

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public Task<Result<SeriesDetail, AeroError>> UpdateAsync(long id, UpdateSeriesRequest request, CancellationToken ct = default)
        => PutAsync<UpdateSeriesRequest, SeriesDetail>(id.ToString(), request, ct);

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync(id.ToString(), ct));

        /// <summary>
    /// EnsureGeneralAsync method.
    /// </summary>
public Task<Result<SeriesDetail, AeroError>> EnsureGeneralAsync(CancellationToken ct = default)
        => PostAsync<object, SeriesDetail>("general", new object(), ct);

        /// <summary>
    /// ListTranslationsAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<SeriesTranslationSummary>, AeroError>> ListTranslationsAsync(long id, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<SeriesTranslationSummary>>($"{id}/translations", ct);

        /// <summary>
    /// UpsertTranslationAsync method.
    /// </summary>
public Task<Result<SeriesTranslationSummary, AeroError>> UpsertTranslationAsync(long id, string culture, UpsertSeriesTranslationRequest request, CancellationToken ct = default)
        => PutAsync<UpsertSeriesTranslationRequest, SeriesTranslationSummary>($"{id}/translations/{Uri.EscapeDataString(culture)}", request, ct);

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

/// <summary>
/// Represents a record for SeriesSummary.
/// </summary>
public record SeriesSummary(long Id, string Name, string Slug, string? Description, int ContentCount);
/// <summary>
/// Represents a record for SeriesDetail.
/// </summary>
public record SeriesDetail(long Id, string Name, string Slug, string? Description, int ContentCount, DateTime CreatedAt);
/// <summary>
/// Represents a record for CreateSeriesRequest.
/// </summary>
public record CreateSeriesRequest(string Name, string Slug, string? Description);
/// <summary>
/// Represents a record for UpdateSeriesRequest.
/// </summary>
public record UpdateSeriesRequest(string Name, string Slug, string? Description);
/// <summary>
/// Represents a record for SeriesTranslationSummary.
/// </summary>
public record SeriesTranslationSummary(string Culture, string Name, string Slug, string? Description, bool HasTranslation, bool IsDefaultCulture);
/// <summary>
/// Represents a record for UpsertSeriesTranslationRequest.
/// </summary>
public record UpsertSeriesTranslationRequest(string Name, string Slug, string? Description);
