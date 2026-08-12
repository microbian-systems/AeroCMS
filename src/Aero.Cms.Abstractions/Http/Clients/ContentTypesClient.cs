using Aero.Cms.Abstractions.Content;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content.Localization;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>
/// Interface for content type definitions HTTP client.
/// </summary>
public interface IContentTypesHttpClient
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<ContentTypeSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// GetByAliasAsync method.
    /// </summary>
Task<Result<ContentTypeDetail, AeroError>> GetByAliasAsync(string alias, CancellationToken ct = default);
    Task<Result<ContentTypeDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<ContentTypeDetail, AeroError>> CreateAsync(CreateContentTypeRequest request, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<ContentTypeDetail, AeroError>> UpdateAsync(string alias, CreateContentTypeRequest request, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(string alias, CancellationToken ct = default);
}

/// <summary>
/// Typed client for content type definitions endpoints.
/// </summary>
public class ContentTypesHttpClient(HttpClient httpClient, ILogger<ContentTypesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IContentTypesHttpClient
{
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public override string Path => "admin/content-types";

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<ContentTypeSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ContentTypeSummary>>(string.Empty, ct);

        /// <summary>
    /// GetByAliasAsync method.
    /// </summary>
public Task<Result<ContentTypeDetail, AeroError>> GetByAliasAsync(string alias, CancellationToken ct = default)
        => GetAsync<ContentTypeDetail>(Uri.EscapeDataString(alias), ct);

    public Task<Result<ContentTypeDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<ContentTypeDetail>($"id/{id.ToString(CultureInfo.InvariantCulture)}", ct);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<Result<ContentTypeDetail, AeroError>> CreateAsync(CreateContentTypeRequest request, CancellationToken ct = default)
        => NormalizeProblemDetails(
            await PostAsync<CreateContentTypeRequest, ContentTypeDetail>(
                string.Empty,
                request,
                ct));

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<Result<ContentTypeDetail, AeroError>> UpdateAsync(string alias, CreateContentTypeRequest request, CancellationToken ct = default)
        => NormalizeProblemDetails(
            await PutAsync<CreateContentTypeRequest, ContentTypeDetail>(
                Uri.EscapeDataString(alias),
                request,
                ct));

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
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

    private static Result<T, AeroError> NormalizeProblemDetails<T>(
        Result<T, AeroError> result)
        where T : class
    {
        if (result is not Result<T, AeroError>.Failure
            {
                Error: AeroError.HttpRequest
                {
                    code: HttpStatusCode.BadRequest,
                    msg: { } responseBody
                }
            }
            || !TryReadProblemDetail(responseBody, out var detail))
        {
            return result;
        }

        return AeroError.ValidationError([detail]);
    }

    private static bool TryReadProblemDetail(
        string responseBody,
        out string detail)
    {
        detail = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("detail", out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            detail = value.GetString()?.Trim() ?? string.Empty;
            return detail.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Interface for content items HTTP client.
/// </summary>
public interface IContentItemsHttpClient
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<PagedResult<ContentItemSummary>, AeroError>> GetAllAsync(string alias, int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);
    /// <summary>Gets bounded options for a searchable or cascading content reference.</summary>
    Task<Result<IReadOnlyList<ContentReferenceOption>, AeroError>> GetReferenceOptionsAsync(
        long targetContentTypeId,
        string? culture = null,
        string? search = null,
        string? filterField = null,
        string? filterValue = null,
        int take = 100,
        CancellationToken ct = default);
    /// <summary>Lists registered CMS document and public content-entry sources.</summary>
    Task<Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>>
        GetCmsReferenceSourcesAsync(CancellationToken ct = default);
    /// <summary>Gets bounded options for one first-class CMS content source.</summary>
    Task<Result<IReadOnlyList<CmsContentReferenceOption>, AeroError>>
        GetCmsReferenceOptionsAsync(
            string source,
            string? culture = null,
            string? search = null,
            int take = 50,
            CancellationToken ct = default);
    /// <summary>Lists current-site virtual entry providers available to content-reference fields.</summary>
    Task<Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>>
        GetContentEntryReferenceSourcesAsync(CancellationToken ct = default);
    /// <summary>Gets bounded current-site options for one exact virtual entry provider.</summary>
    Task<Result<IReadOnlyList<ContentEntryReferenceOption>, AeroError>>
        GetContentEntryReferenceOptionsAsync(
            string provider,
            string? culture = null,
            string? search = null,
            int take = 50,
            CancellationToken ct = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<Result<ContentItemDetail, AeroError>> GetByIdAsync(string alias, long id, CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<ContentItemDetail, AeroError>> CreateAsync(string alias, CreateContentItemRequest request, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<ContentItemDetail, AeroError>> UpdateAsync(string alias, long id, CreateContentItemRequest request, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(string alias, long id, CancellationToken ct = default);
        /// <summary>
    /// PublishAsync method.
    /// </summary>
Task<Result<ContentItemDetail, AeroError>> PublishAsync(string alias, long id, CancellationToken ct = default);
        /// <summary>
    /// UnpublishAsync method.
    /// </summary>
Task<Result<ContentItemDetail, AeroError>> UnpublishAsync(string alias, long id, CancellationToken ct = default);
        /// <summary>
    /// GetTranslationsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<ContentItemDetail>, AeroError>> GetTranslationsAsync(string alias, long id, CancellationToken ct = default);
        /// <summary>
    /// ForkToCultureAsync method.
    /// </summary>
Task<Result<ContentItemDetail, AeroError>> ForkToCultureAsync(string alias, long id, ForkContentItemCultureRequest request, CancellationToken ct = default);
    /// <summary>Gets the bounded manager hierarchy for a content type and culture.</summary>
    Task<Result<ContentHierarchyTreeResult, AeroError>> GetHierarchyAsync(
        string alias,
        string? culture = null,
        CancellationToken ct = default);
    /// <summary>Atomically moves an item and normalizes both affected sibling collections.</summary>
    Task<Result<ContentHierarchyTreeResult, AeroError>> MoveAsync(
        string alias,
        long id,
        MoveContentItemRequest request,
        CancellationToken ct = default);
    /// <summary>Atomically replaces the order of one exact sibling collection.</summary>
    Task<Result<ContentHierarchyTreeResult, AeroError>> ReorderAsync(
        string alias,
        ReorderContentSiblingsRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Typed client for content items endpoints.
/// </summary>
public class ContentItemsHttpClient(HttpClient httpClient, ILogger<ContentItemsHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IContentItemsHttpClient
{
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public override string Path => "admin/content-items";

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public Task<Result<PagedResult<ContentItemSummary>, AeroError>> GetAllAsync(string alias, int skip = 0, int take = 10, string? search = null, CancellationToken ct = default)
    {
        var url = $"?contentType={Uri.EscapeDataString(alias)}&skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<ContentItemSummary>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ContentReferenceOption>, AeroError>> GetReferenceOptionsAsync(
        long targetContentTypeId,
        string? culture = null,
        string? search = null,
        string? filterField = null,
        string? filterValue = null,
        int take = 100,
        CancellationToken ct = default)
    {
        var parameters = new List<string> { $"take={Math.Clamp(take, 1, 100)}" };
        AddQueryParameter(parameters, "culture", culture);
        AddQueryParameter(parameters, "search", search);
        AddQueryParameter(parameters, "filterField", filterField);
        AddQueryParameter(parameters, "filterValue", filterValue);
        var url =
            $"reference-options/{targetContentTypeId.ToString(CultureInfo.InvariantCulture)}?{string.Join("&", parameters)}";
        return GetAsync<IReadOnlyList<ContentReferenceOption>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>>
        GetCmsReferenceSourcesAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<CmsContentReferenceSource>>(
            "reference-sources",
            ct);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CmsContentReferenceOption>, AeroError>>
        GetCmsReferenceOptionsAsync(
            string source,
            string? culture = null,
            string? search = null,
            int take = 50,
            CancellationToken ct = default)
    {
        var parameters = new List<string>
        {
            $"take={Math.Clamp(take, 1, 100)}"
        };
        AddQueryParameter(parameters, "culture", culture);
        AddQueryParameter(parameters, "search", search);
        return GetAsync<IReadOnlyList<CmsContentReferenceOption>>(
            $"reference-sources/{Uri.EscapeDataString(source)}/options?{string.Join("&", parameters)}",
            ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>>
        GetContentEntryReferenceSourcesAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<CmsContentReferenceSource>>("entry-reference-sources", ct);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ContentEntryReferenceOption>, AeroError>>
        GetContentEntryReferenceOptionsAsync(
            string provider,
            string? culture = null,
            string? search = null,
            int take = 50,
            CancellationToken ct = default)
    {
        var parameters = new List<string> { $"take={Math.Clamp(take, 1, 100)}" };
        AddQueryParameter(parameters, "culture", culture);
        AddQueryParameter(parameters, "search", search);
        return GetAsync<IReadOnlyList<ContentEntryReferenceOption>>(
            $"entry-reference-sources/{Uri.EscapeDataString(provider)}/options?{string.Join("&", parameters)}",
            ct);
    }

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public Task<Result<ContentItemDetail, AeroError>> GetByIdAsync(string alias, long id, CancellationToken ct = default)
        => GetAsync<ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}", ct);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public Task<Result<ContentItemDetail, AeroError>> CreateAsync(string alias, CreateContentItemRequest request, CancellationToken ct = default)
        => PostAsync<CreateContentItemRequest, ContentItemDetail>(Uri.EscapeDataString(alias), request, ct);

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public Task<Result<ContentItemDetail, AeroError>> UpdateAsync(string alias, long id, CreateContentItemRequest request, CancellationToken ct = default)
        => PutAsync<CreateContentItemRequest, ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}", request, ct);

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public Task<Result<bool, AeroError>> DeleteAsync(string alias, long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync($"{Uri.EscapeDataString(alias)}/{id}", ct));

        /// <summary>
    /// PublishAsync method.
    /// </summary>
public Task<Result<ContentItemDetail, AeroError>> PublishAsync(string alias, long id, CancellationToken ct = default)
        => PostAsync<object, ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}/publish", new object(), ct);

        /// <summary>
    /// UnpublishAsync method.
    /// </summary>
public Task<Result<ContentItemDetail, AeroError>> UnpublishAsync(string alias, long id, CancellationToken ct = default)
        => PostAsync<object, ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}/unpublish", new object(), ct);

        /// <summary>
    /// GetTranslationsAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<ContentItemDetail>, AeroError>> GetTranslationsAsync(string alias, long id, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ContentItemDetail>>($"{Uri.EscapeDataString(alias)}/{id}/translations", ct);

        /// <summary>
    /// ForkToCultureAsync method.
    /// </summary>
public Task<Result<ContentItemDetail, AeroError>> ForkToCultureAsync(string alias, long id, ForkContentItemCultureRequest request, CancellationToken ct = default)
        => PostAsync<ForkContentItemCultureRequest, ContentItemDetail>($"{Uri.EscapeDataString(alias)}/{id}/translations", request, ct);

    /// <inheritdoc />
    public Task<Result<ContentHierarchyTreeResult, AeroError>> GetHierarchyAsync(
        string alias,
        string? culture = null,
        CancellationToken ct = default)
    {
        var url = $"{Uri.EscapeDataString(alias)}/hierarchy";
        if (!string.IsNullOrWhiteSpace(culture))
        {
            url += $"?culture={Uri.EscapeDataString(culture)}";
        }

        return GetAsync<ContentHierarchyTreeResult>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<ContentHierarchyTreeResult, AeroError>> MoveAsync(
        string alias,
        long id,
        MoveContentItemRequest request,
        CancellationToken ct = default)
        => PutAsync<MoveContentItemRequest, ContentHierarchyTreeResult>(
            $"{Uri.EscapeDataString(alias)}/{id}/move",
            request,
            ct);

    /// <inheritdoc />
    public Task<Result<ContentHierarchyTreeResult, AeroError>> ReorderAsync(
        string alias,
        ReorderContentSiblingsRequest request,
        CancellationToken ct = default)
        => PutAsync<ReorderContentSiblingsRequest, ContentHierarchyTreeResult>(
            $"{Uri.EscapeDataString(alias)}/hierarchy/reorder",
            request,
            ct);

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

    private static void AddQueryParameter(
        ICollection<string> parameters,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(
                $"{name}={Uri.EscapeDataString(value.Trim())}");
        }
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
    bool AllowPublicUrl,
    bool IncludeInSearch,
    bool IncludeInPublicAi,
    int FieldCount,
    bool HasCustomTemplate,
    long ItemCount,
    long Id = 0,
    ContentCardinality Cardinality = ContentCardinality.Collection,
    ContentStructure Structure = ContentStructure.Flat,
    ContentHierarchyRules? HierarchyRules = null);

/// <summary>Detailed information for a content type definition.</summary>
public record ContentTypeDetail(
    string Alias,
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    bool AllowPublicUrl,
    bool IncludeInSearch,
    bool IncludeInPublicAi,
    IReadOnlyList<ContentFieldDefinition> Fields,
    string? ScribanTemplate,
    ContentTypeScheduleConfig? ScheduleConfig,
    long Id = 0,
    ContentCardinality Cardinality = ContentCardinality.Collection,
    ContentStructure Structure = ContentStructure.Flat,
    ContentHierarchyRules? HierarchyRules = null);

/// <summary>Request to create or update a content type definition.</summary>
public record CreateContentTypeRequest(
    string Alias,
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    bool AllowPublicUrl,
    bool IncludeInSearch,
    bool IncludeInPublicAi,
    IReadOnlyList<ContentFieldDefinition> Fields,
    string? ScribanTemplate,
    ContentTypeScheduleConfig? ScheduleConfig,
    ContentCardinality Cardinality = ContentCardinality.Collection,
    ContentStructure Structure = ContentStructure.Flat,
    ContentHierarchyRules? HierarchyRules = null);

/// <summary>Summary information for a content item.</summary>
public record ContentItemSummary(
    long Id,
    string Title,
    string Slug,
    string ContentTypeAlias,
    string? FirstFieldValue,
    string PublicationState,
    DateTimeOffset? PublishedOn,
    int VersionNumber,
    string Culture,
    long? TranslationGroupId,
    long? SourceItemId,
    long? ParentId = null,
    int SortOrder = 0,
    ContentTranslationProvenance? TranslationProvenance = null,
    ContentTranslationReview? TranslationReview = null,
    int? TranslationGroupRevision = null);

/// <summary>A bounded manager-facing option for a content reference field.</summary>
public sealed record ContentReferenceOption(
    long Id,
    string Title,
    string Slug,
    string Culture);

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
    DateTimeOffset? ScheduleUnpublishUtc,
    string Culture,
    long? TranslationGroupId,
    long? SourceItemId,
    long? ParentId = null,
    int SortOrder = 0,
    ContentTranslationProvenance? TranslationProvenance = null,
    ContentTranslationReview? TranslationReview = null,
    int? TranslationGroupRevision = null);

/// <summary>Request to create or update a content item.</summary>
public record CreateContentItemRequest(
    string Title,
    string Slug,
    IReadOnlyDictionary<string, JsonElement> Fields,
    DateTimeOffset? SchedulePublishUtc,
    DateTimeOffset? ScheduleUnpublishUtc,
    string? Culture = null,
    long? ParentId = null,
    int SortOrder = 0);

/// <summary>
/// Represents a record for ForkContentItemCultureRequest.
/// </summary>
public record ForkContentItemCultureRequest(string Culture, string Slug);

/// <summary>Applies AI-translated fields to an existing culture variant with revision fencing.</summary>
public sealed record ApplyContentItemAiTranslationRequest(
    long TargetItemId,
    int SourceVersionNumber,
    int ExpectedTargetVersionNumber,
    string SourceCulture,
    string TargetCulture,
    IReadOnlyDictionary<string, JsonElement> TranslatedFields,
    string ProviderId,
    string Model);

/// <summary>Records a human translation review against current source and target versions.</summary>
public sealed record ReviewContentItemTranslationRequest(
    long TargetItemId,
    int SourceVersionNumber,
    int TargetVersionNumber,
    bool Approved,
    string? Notes = null);

/// <summary>One immutable manager-facing node in a bounded content hierarchy.</summary>
public sealed record ContentHierarchyTreeNode(
    long Id,
    string Title,
    string Slug,
    string ContentTypeAlias,
    string Culture,
    string PublicationState,
    long? ParentId,
    int SortOrder,
    int Depth,
    bool IsTargetType,
    bool CanAcceptChildren,
    IReadOnlyList<ContentHierarchyTreeNode> Children);

/// <summary>A bounded, pre-shaped manager hierarchy selected for one type and culture.</summary>
public sealed record ContentHierarchyTreeResult(
    string ContentTypeAlias,
    string Culture,
    int TotalCount,
    IReadOnlyList<ContentHierarchyTreeNode> Roots);

/// <summary>Moves one item to a parent and zero-based position in one transaction.</summary>
public sealed record MoveContentItemRequest(
    long? NewParentId,
    int TargetIndex,
    string? Culture = null);

/// <summary>Replaces one exact sibling order in one transaction.</summary>
public sealed record ReorderContentSiblingsRequest(
    long? ParentId,
    IReadOnlyList<long> OrderedIds,
    string? Culture = null);
