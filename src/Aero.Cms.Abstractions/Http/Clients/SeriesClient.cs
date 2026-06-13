namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface ISeriesHttpClient
{
    Task<Result<IReadOnlyList<SeriesSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);
    Task<Result<SeriesDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<SeriesDetail, AeroError>> CreateAsync(CreateSeriesRequest request, CancellationToken ct = default);
    Task<Result<SeriesDetail, AeroError>> UpdateAsync(long id, UpdateSeriesRequest request, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
    Task<Result<SeriesDetail, AeroError>> EnsureGeneralAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<SeriesTranslationSummary>, AeroError>> ListTranslationsAsync(long id, CancellationToken ct = default);
    Task<Result<SeriesTranslationSummary, AeroError>> UpsertTranslationAsync(long id, string culture, UpsertSeriesTranslationRequest request, CancellationToken ct = default);
}

public sealed class SeriesHttpClient(HttpClient httpClient, ILogger<SeriesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ISeriesHttpClient
{
    public override string Path => "admin/series";

    public Task<Result<IReadOnlyList<SeriesSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<SeriesSummary>>(string.Empty, ct);

    public Task<Result<SeriesDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<SeriesDetail>($"details/{id}", ct);

    public Task<Result<SeriesDetail, AeroError>> CreateAsync(CreateSeriesRequest request, CancellationToken ct = default)
        => PostAsync<CreateSeriesRequest, SeriesDetail>(string.Empty, request, ct);

    public Task<Result<SeriesDetail, AeroError>> UpdateAsync(long id, UpdateSeriesRequest request, CancellationToken ct = default)
        => PutAsync<UpdateSeriesRequest, SeriesDetail>(id.ToString(), request, ct);

    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync(id.ToString(), ct));

    public Task<Result<SeriesDetail, AeroError>> EnsureGeneralAsync(CancellationToken ct = default)
        => PostAsync<object, SeriesDetail>("general", new object(), ct);

    public Task<Result<IReadOnlyList<SeriesTranslationSummary>, AeroError>> ListTranslationsAsync(long id, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<SeriesTranslationSummary>>($"{id}/translations", ct);

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

public record SeriesSummary(long Id, string Name, string Slug, string? Description, int ContentCount);
public record SeriesDetail(long Id, string Name, string Slug, string? Description, int ContentCount, DateTime CreatedAt);
public record CreateSeriesRequest(string Name, string Slug, string? Description);
public record UpdateSeriesRequest(string Name, string Slug, string? Description);
public record SeriesTranslationSummary(string Culture, string Name, string Slug, string? Description, bool HasTranslation, bool IsDefaultCulture);
public record UpsertSeriesTranslationRequest(string Name, string Slug, string? Description);
