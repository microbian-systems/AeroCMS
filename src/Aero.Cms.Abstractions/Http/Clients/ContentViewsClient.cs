using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content.Views;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>Site-scoped manager client for query-backed content views.</summary>
public interface IContentViewsHttpClient
{
    Task<Result<IReadOnlyList<ContentViewShapeOption>, AeroError>> GetShapesAsync(CancellationToken ct = default);
    Task<Result<ContentViewEditorSnapshot, AeroError>> GetAsync(string alias, CancellationToken ct = default);
    Task<Result<ContentViewEditorSnapshot, AeroError>> SaveDraftAsync(string alias, SaveContentViewDraftRequest request, CancellationToken ct = default);
    Task<Result<ContentViewPreviewResponse, AeroError>> PreviewAsync(string alias, PreviewContentViewRequest request, CancellationToken ct = default);
    Task<Result<ContentViewEditorSnapshot, AeroError>> PublishAsync(string alias, long draftVersion, CancellationToken ct = default);
    Task<Result<bool, AeroError>> InvalidateCacheAsync(string alias, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ContentRelationshipSummary>, AeroError>> GetRelationshipsAsync(string alias, CancellationToken ct = default);
    Task<Result<ContentRelationshipSummary, AeroError>> SaveRelationshipDraftAsync(string alias, string relationshipAlias, SaveContentRelationshipDraftRequest request, CancellationToken ct = default);
    Task<Result<RelationshipDdlPreviewResponse, AeroError>> PreviewRelationshipDdlAsync(string alias, long relationshipId, CancellationToken ct = default);
    Task<Result<RelationshipDdlApplyResponse, AeroError>> ApplyRelationshipDdlAsync(string alias, long relationshipId, string proposedSchemaFingerprint, CancellationToken ct = default);
    Task<Result<IReadOnlyList<VirtualContentEntryOption>, AeroError>> SearchEntriesAsync(string provider, string? culture = null, string? query = null, int take = 50, CancellationToken ct = default);
    Task<Result<VirtualContentEntryDetail, AeroError>> GetEntryAsync(string provider, string stableId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ContentEntryProviderOption>, AeroError>> GetEntryProvidersAsync(CancellationToken ct = default);
}

/// <summary>HTTP implementation of the content-view manager contract.</summary>
public sealed class ContentViewsHttpClient(HttpClient httpClient, ILogger<ContentViewsHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IContentViewsHttpClient
{
    public override string Path => "admin/content-views";

    public Task<Result<IReadOnlyList<ContentViewShapeOption>, AeroError>> GetShapesAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ContentViewShapeOption>>("shapes", ct);

    public Task<Result<ContentViewEditorSnapshot, AeroError>> GetAsync(string alias, CancellationToken ct = default)
        => GetAsync<ContentViewEditorSnapshot>(Uri.EscapeDataString(alias), ct);

    public Task<Result<ContentViewEditorSnapshot, AeroError>> SaveDraftAsync(
        string alias,
        SaveContentViewDraftRequest request,
        CancellationToken ct = default)
        => PutAsync<SaveContentViewDraftRequest, ContentViewEditorSnapshot>(
            $"{Uri.EscapeDataString(alias)}/draft",
            request,
            ct);

    public Task<Result<ContentViewPreviewResponse, AeroError>> PreviewAsync(
        string alias,
        PreviewContentViewRequest request,
        CancellationToken ct = default)
        => PostAsync<PreviewContentViewRequest, ContentViewPreviewResponse>(
            $"{Uri.EscapeDataString(alias)}/preview",
            request,
            ct);

    public Task<Result<ContentViewEditorSnapshot, AeroError>> PublishAsync(string alias, long draftVersion, CancellationToken ct = default)
        => PostAsync<PublishContentViewRequest, ContentViewEditorSnapshot>(
            $"{Uri.EscapeDataString(alias)}/publish",
            new PublishContentViewRequest(draftVersion),
            ct);

    public async Task<Result<bool, AeroError>> InvalidateCacheAsync(string alias, CancellationToken ct = default)
    {
        var result = await PostAsync<object, ContentViewCacheInvalidationResponse>(
            $"{Uri.EscapeDataString(alias)}/cache/invalidate",
            new object(),
            ct);
        return result switch
        {
            Result<ContentViewCacheInvalidationResponse, AeroError>.Ok ok => ok.Value.Invalidated,
            Result<ContentViewCacheInvalidationResponse, AeroError>.Failure failure => failure.Error,
            _ => AeroError.CreateError("Unexpected cache invalidation result.")
        };
    }

    public Task<Result<IReadOnlyList<ContentRelationshipSummary>, AeroError>> GetRelationshipsAsync(
        string alias,
        CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ContentRelationshipSummary>>(
            $"{Uri.EscapeDataString(alias)}/relationships",
            ct);

    public Task<Result<ContentRelationshipSummary, AeroError>> SaveRelationshipDraftAsync(
        string alias,
        string relationshipAlias,
        SaveContentRelationshipDraftRequest request,
        CancellationToken ct = default)
        => PutAsync<SaveContentRelationshipDraftRequest, ContentRelationshipSummary>(
            $"{Uri.EscapeDataString(alias)}/relationships/{Uri.EscapeDataString(relationshipAlias)}/draft",
            request,
            ct);

    public Task<Result<RelationshipDdlPreviewResponse, AeroError>> PreviewRelationshipDdlAsync(
        string alias,
        long relationshipId,
        CancellationToken ct = default)
        => PostAsync<object, RelationshipDdlPreviewResponse>(
            $"{Uri.EscapeDataString(alias)}/relationships/{relationshipId.ToString(CultureInfo.InvariantCulture)}/ddl/preview",
            new object(),
            ct);

    public Task<Result<RelationshipDdlApplyResponse, AeroError>> ApplyRelationshipDdlAsync(
        string alias,
        long relationshipId,
        string proposedSchemaFingerprint,
        CancellationToken ct = default)
        => PostAsync<ApplyRelationshipDdlRequest, RelationshipDdlApplyResponse>(
            $"{Uri.EscapeDataString(alias)}/relationships/{relationshipId.ToString(CultureInfo.InvariantCulture)}/ddl/apply",
            new ApplyRelationshipDdlRequest(proposedSchemaFingerprint),
            ct);

    public Task<Result<IReadOnlyList<VirtualContentEntryOption>, AeroError>> SearchEntriesAsync(
        string provider,
        string? culture = null,
        string? query = null,
        int take = 50,
        CancellationToken ct = default)
    {
        var parameters = new List<string> { $"take={Math.Clamp(take, 1, 100)}" };
        if (!string.IsNullOrWhiteSpace(culture)) parameters.Add($"culture={Uri.EscapeDataString(culture.Trim())}");
        if (!string.IsNullOrWhiteSpace(query)) parameters.Add($"query={Uri.EscapeDataString(query.Trim())}");
        return GetAsync<IReadOnlyList<VirtualContentEntryOption>>(
            $"entries/{Uri.EscapeDataString(provider)}?{string.Join("&", parameters)}",
            ct);
    }

    public Task<Result<VirtualContentEntryDetail, AeroError>> GetEntryAsync(
        string provider,
        string stableId,
        CancellationToken ct = default)
        => GetAsync<VirtualContentEntryDetail>(
            $"entries/{Uri.EscapeDataString(provider)}/{Uri.EscapeDataString(stableId)}",
            ct);

    public Task<Result<IReadOnlyList<ContentEntryProviderOption>, AeroError>> GetEntryProvidersAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ContentEntryProviderOption>>("entries", ct);
}

/// <summary>Code-owned shape exposed to the content-view editor.</summary>
public sealed record ContentViewShapeOption(
    string Alias,
    string SchemaFingerprint,
    IReadOnlyList<ContentShapeField> Fields);

/// <summary>Persisted editor projection. Scope is intentionally absent because the server owns it.</summary>
public sealed record ContentViewEditorSnapshot(
    string Alias,
    string ShapeAlias,
    string ShapeFingerprint,
    string SelectStatement,
    string IdentityField,
    string? TitleField,
    string EntrySelectStatement,
    string SearchSelectStatement,
    long Version,
    ContentViewPublicationState PublicationState,
    bool CacheEnabled = true,
    int CacheDurationSeconds = 300,
    long CacheGeneration = 0,
    bool PublicExecutionEligible = false,
    string? PublicExecutionIneligibilityReason = null);

/// <summary>Draft update. Tenant and site identifiers must never be accepted from the client.</summary>
public sealed record SaveContentViewDraftRequest(
    string ShapeAlias,
    string SelectStatement,
    string IdentityField,
    string? TitleField,
    string EntrySelectStatement,
    string SearchSelectStatement,
    bool CacheEnabled = true,
    int CacheDurationSeconds = 300);

/// <summary>Bounded preview request for an unsaved editor query.</summary>
public sealed record PreviewContentViewRequest(
    string ShapeAlias,
    string SelectStatement,
    int Take = 20);

public sealed record PublishContentViewRequest(long DraftVersion);

public sealed record ContentViewPreviewResponse(
    IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> Rows,
    IReadOnlyList<string> OutputFields,
    bool IsTruncated);

public sealed record ContentViewCacheInvalidationResponse(bool Invalidated, long Generation);

public sealed record ContentRelationshipSummary(
    long Id,
    string Alias,
    string? SourceShapeAlias,
    string? TargetShapeAlias,
    string SourceTable,
    string TargetTable,
    string? SourceField,
    string? TargetField,
    string? EdgeTable,
    ContentRelationshipKind Kind,
    ContentRelationshipCardinality Cardinality,
    ContentRelationshipOwnershipState OwnershipState,
    string SchemaFingerprint,
    bool CanPreviewDdl,
    bool CanApplyDdl);

/// <summary>Editable CMS relationship metadata. Scope, ownership, identifiers, and fingerprints remain server-owned.</summary>
public sealed record SaveContentRelationshipDraftRequest(
    string? SourceShapeAlias,
    string? TargetShapeAlias,
    string SourceTable,
    string TargetTable,
    string? SourceField,
    string? TargetField,
    string? EdgeTable,
    ContentRelationshipKind Kind,
    ContentRelationshipCardinality Cardinality);

/// <summary>Client-only envelope used by the relationship editor.</summary>
public sealed record SaveContentRelationshipDraftCommand(
    string Alias,
    SaveContentRelationshipDraftRequest Request);

public sealed record RelationshipDdlPreviewResponse(
    long RelationshipId,
    string ProposedSchemaFingerprint,
    IReadOnlyList<string> Statements);

public sealed record ApplyRelationshipDdlRequest(string ProposedSchemaFingerprint);

public sealed record RelationshipDdlApplyResponse(
    long RelationshipId,
    string AppliedSchemaFingerprint,
    DateTimeOffset AppliedOn,
    string? AppliedBy);

public sealed record VirtualContentEntryOption(
    string Provider,
    string StableId,
    string Title,
    string? Subtitle);

public sealed record ContentEntryProviderOption(string Provider, string DisplayName);

public sealed record VirtualContentEntryDetail(
    string Provider,
    string StableId,
    IReadOnlyDictionary<string, JsonElement> Values);
