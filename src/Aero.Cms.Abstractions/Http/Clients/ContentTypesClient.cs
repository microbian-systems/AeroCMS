using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>
/// Interface for content type definitions HTTP client.
/// </summary>
public interface IContentTypesHttpClient
{
    Task<Result<IReadOnlyList<ContentTypeSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ContentTypeDetail, AeroError>> GetByAliasAsync(string alias, CancellationToken ct = default);
    Task<Result<ContentTypeDetail, AeroError>> CreateAsync(CreateContentTypeRequest request, CancellationToken ct = default);
    Task<Result<ContentTypeDetail, AeroError>> UpdateAsync(string alias, CreateContentTypeRequest request, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(string alias, CancellationToken ct = default);
}

/// <summary>
/// Typed client for content type definitions endpoints.
/// </summary>
public class ContentTypesHttpClient(HttpClient httpClient, ILogger<ContentTypesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IContentTypesHttpClient
{
    public override string Path => "admin/content-types";

    public Task<Result<IReadOnlyList<ContentTypeSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ContentTypeSummary>>(string.Empty, ct);

    public Task<Result<ContentTypeDetail, AeroError>> GetByAliasAsync(string alias, CancellationToken ct = default)
        => GetAsync<ContentTypeDetail>(Uri.EscapeDataString(alias), ct);

    public Task<Result<ContentTypeDetail, AeroError>> CreateAsync(CreateContentTypeRequest request, CancellationToken ct = default)
        => PostAsync<CreateContentTypeRequest, ContentTypeDetail>(string.Empty, request, ct);

    public Task<Result<ContentTypeDetail, AeroError>> UpdateAsync(string alias, CreateContentTypeRequest request, CancellationToken ct = default)
        => PutAsync<CreateContentTypeRequest, ContentTypeDetail>(Uri.EscapeDataString(alias), request, ct);

    public Task<Result<bool, AeroError>> DeleteAsync(string alias, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync(Uri.EscapeDataString(alias), ct));

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
/// Interface for content items HTTP client.
/// </summary>
public interface IContentItemsHttpClient
{
    Task<Result<PagedResult<ContentItemSummary>, AeroError>> GetAllAsync(string alias, int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);
    Task<Result<ContentItemDetail, AeroError>> GetByIdAsync(string alias, long id, CancellationToken ct = default);
    Task<Result<ContentItemDetail, AeroError>> CreateAsync(string alias, CreateContentItemRequest request, CancellationToken ct = default);
    Task<Result<ContentItemDetail, AeroError>> UpdateAsync(string alias, long id, CreateContentItemRequest request, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(string alias, long id, CancellationToken ct = default);
    Task<Result<ContentItemDetail, AeroError>> PublishAsync(string alias, long id, CancellationToken ct = default);
    Task<Result<ContentItemDetail, AeroError>> UnpublishAsync(string alias, long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for content items endpoints.
/// </summary>
public class ContentItemsHttpClient(HttpClient httpClient, ILogger<ContentItemsHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IContentItemsHttpClient
{
    public override string Path => "admin/content-items";

    public Task<Result<PagedResult<ContentItemSummary>, AeroError>> GetAllAsync(string alias, int skip = 0, int take = 10, string? search = null, CancellationToken ct = default)
    {
        var url = $"?contentType={Uri.EscapeDataString(alias)}&skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<ContentItemSummary>>(url, ct);
    }

    public Task<Result<ContentItemDetail, AeroError>> GetByIdAsync(string alias, long id, CancellationToken ct = default)
        => GetAsync<ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}", ct);

    public Task<Result<ContentItemDetail, AeroError>> CreateAsync(string alias, CreateContentItemRequest request, CancellationToken ct = default)
        => PostAsync<CreateContentItemRequest, ContentItemDetail>(Uri.EscapeDataString(alias), request, ct);

    public Task<Result<ContentItemDetail, AeroError>> UpdateAsync(string alias, long id, CreateContentItemRequest request, CancellationToken ct = default)
        => PutAsync<CreateContentItemRequest, ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}", request, ct);

    public Task<Result<bool, AeroError>> DeleteAsync(string alias, long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync($"{Uri.EscapeDataString(alias)}/{id}", ct));

    public Task<Result<ContentItemDetail, AeroError>> PublishAsync(string alias, long id, CancellationToken ct = default)
        => PutAsync<object, ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}/publish", new object(), ct);

    public Task<Result<ContentItemDetail, AeroError>> UnpublishAsync(string alias, long id, CancellationToken ct = default)
        => PutAsync<object, ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}/unpublish", new object(), ct);

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

// ── DTOs ──────────────────────────────────────────────────────

/// <summary>Summary information for a content type definition.</summary>
public record ContentTypeSummary(
    string Alias,
    string Name,
    string? Description,
    string? Category,
    int FieldCount,
    string RenderMode,
    bool HasCustomTemplate,
    long ItemCount);

/// <summary>Detailed information for a content type definition.</summary>
public record ContentTypeDetail(
    string Alias,
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    IReadOnlyList<ContentFieldDefinition> Fields,
    string? ScribanTemplate,
    string RenderMode,
    ContentTypeScheduleConfig? ScheduleConfig);

/// <summary>Request to create or update a content type definition.</summary>
public record CreateContentTypeRequest(
    string Alias,
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    IReadOnlyList<ContentFieldDefinition> Fields,
    string? ScribanTemplate,
    string RenderMode,
    ContentTypeScheduleConfig? ScheduleConfig);

/// <summary>Summary information for a content item.</summary>
public record ContentItemSummary(
    long Id,
    string Title,
    string Slug,
    string ContentTypeAlias,
    string? FirstFieldValue,
    string PublicationState,
    DateTimeOffset? PublishedOn,
    int VersionNumber);

/// <summary>Detailed information for a content item.</summary>
public record ContentItemDetail(
    long Id,
    string Title,
    string Slug,
    string ContentTypeAlias,
    IReadOnlyDictionary<string, System.Text.Json.JsonElement> Fields,
    string PublicationState,
    DateTimeOffset? PublishedOn,
    int VersionNumber,
    DateTimeOffset? SchedulePublishUtc,
    DateTimeOffset? ScheduleUnpublishUtc);

/// <summary>Request to create or update a content item.</summary>
public record CreateContentItemRequest(
    string Title,
    string Slug,
    IReadOnlyDictionary<string, object?> Fields,
    DateTimeOffset? SchedulePublishUtc,
    DateTimeOffset? ScheduleUnpublishUtc);
