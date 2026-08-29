using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aero.Cms.Core.Content.Services;

/// <summary>Application service for site-scoped view drafts, publication, preview, and public read-only execution.</summary>
public sealed class ContentSurrealViewService(
    IContentSurrealViewStore store,
    IContentViewCacheInvalidator cacheInvalidator,
    IReadOnlyContentViewExecutor executor,
    IContentViewStatementClassifier classifier,
    IContentViewScopeBinder scopeBinder,
    IContentShapeRegistry shapeRegistry,
    IContentViewSourceRegistry sourceRegistry,
    IContentViewExecutionCache executionCache,
    IContentViewCacheGenerationProvider cacheGenerationProvider,
    IAdminReadOnlyContentViewExecutor? adminExecutor = null,
    IContentViewOutputCacheInvalidator? outputCacheInvalidator = null,
    IContentViewDistributedCacheCoordinator? distributedCacheCoordinator = null,
    IContentViewTrustedQueryPlanRegistry? relationshipPlans = null,
    IContentViewRelationshipPlanDialectCapability? relationshipDialect = null) : IContentSurrealViewService
{
    public Task<ContentSurrealViewRevision?> LoadPublishedAsync(ContentViewScope scope, string alias, CancellationToken ct = default)
        => scope.IsValid && !string.IsNullOrWhiteSpace(alias)
            ? store.LoadAsync(scope, alias, ContentViewPublicationState.Published, ct)
            : Task.FromResult<ContentSurrealViewRevision?>(null);

    public Task<ContentSurrealViewRevision?> LoadDraftAsync(ContentViewScope scope, string alias, CancellationToken ct = default)
        => scope.IsValid && !string.IsNullOrWhiteSpace(alias)
            ? store.LoadAsync(scope, alias, ContentViewPublicationState.Draft, ct)
            : Task.FromResult<ContentSurrealViewRevision?>(null);

    public async Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid) return;
        await cacheInvalidator.InvalidateAsync(scope, ct);
        if (distributedCacheCoordinator is { IsDistributed: true })
            await distributedCacheCoordinator.InvalidateAsync(scope, ct);
        await (outputCacheInvalidator ?? new DisabledContentViewOutputCacheInvalidator()).InvalidateAsync(scope, ct);
    }
    public async Task<ContentSurrealViewRevision?> SaveDraftAsync(ContentSurrealViewRevision draft, int maximumPreviewTake, CancellationToken ct = default)
    {
        if (!draft.Scope.IsValid || !draft.HasEntryIdentity || draft.PublicationState != ContentViewPublicationState.Draft
            || !shapeRegistry.TryGet(draft.ShapeAlias, out var shape)
            || !string.Equals(shape!.SchemaFingerprint, draft.ShapeFingerprint, StringComparison.Ordinal)
            || !IsValidIdentityField(shape!, draft.IdentityField)
            || draft.TitleField is not null && !shape.Fields.Any(x => string.Equals(x.Name, draft.TitleField, StringComparison.Ordinal))
            || !IsAdminReadOnlyStatement(draft.SelectStatement)) return null;

        var eligible = IsSafeStatement(draft.SelectStatement, maximumPreviewTake, out _)
            && ValidatePublishedEntryStatements(draft, maximumPreviewTake);
        if (HasPublicRelationshipPlan(draft) && !HasVerifiedRelationshipPlan(draft))
            eligible = false;
        draft = draft with { PublicExecutionEligible = eligible,
            PublicExecutionIneligibilityReason = eligible ? null : "This read can be previewed by an administrator but is not eligible for public execution." };

        var saved = await store.SaveDraftAsync(draft, ct);
        await InvalidateAsync(saved.Scope, ct);
        return saved;
    }

    public async Task<ContentSurrealViewRevision?> PublishAsync(ContentViewScope scope, string alias, long draftVersion, CancellationToken ct = default)
    {
        if (!scope.IsValid || string.IsNullOrWhiteSpace(alias) || draftVersion <= 0) return null;
        var draft = await store.LoadAsync(scope, alias, ContentViewPublicationState.Draft, ct);
        if (draft is null || !draft.PublicExecutionEligible || draft.Version != draftVersion || !shapeRegistry.TryGet(draft.ShapeAlias, out var shape)
            || !string.Equals(shape!.SchemaFingerprint, draft.ShapeFingerprint, StringComparison.Ordinal)
            || !IsSafeStatement(draft.SelectStatement, 100, out _)
            || !ValidatePublishedEntryStatements(draft, 100)) return null;
        var published = await store.PublishAsync(scope, alias, draftVersion, ct);
        if (published is not null) await InvalidateAsync(scope, ct);
        return published;
    }

    public async Task<ContentViewExecutionResult?> PreviewAsync(
        ContentSurrealViewRevision draft,
        ContentViewScope scope,
        IReadOnlyDictionary<string, object?> callerParameters,
        int take,
        int maximumPreviewTake,
        CancellationToken ct = default)
    {
        if (draft.Scope != scope || take <= 0 || take > maximumPreviewTake || adminExecutor is not { IsReadOnlyGuaranteed: true }
            || !TryGetAdminReadOnlySource(draft.SelectStatement, out var source)
            || !scopeBinder.TryBind(scope, callerParameters, out var bound)) return null;
        var limits = new ContentViewExecutionLimits(maximumPreviewTake, maximumPreviewTake);
        var result = await adminExecutor.ExecuteAsync(draft, scope, bound, limits, ct);
        var bounded = ContentViewExecutionLimitEnforcer.Enforce(result, take, limits);
        bounded = ProjectDeclaredSourceFields(bounded, source!);
        if (!shapeRegistry.TryGet(draft.ShapeAlias, out var shape) || !ContentShapeRowValidator.TryValidateRows(bounded.Rows, shape!, out _)) return null;
        return bounded;
    }

    public async Task<ContentViewExecutionResult?> ExecutePublicAsync(
        ContentViewScope scope,
        string alias,
        IReadOnlyDictionary<string, object?> callerParameters,
        int take,
        CancellationToken ct = default)
    {
        var published = await store.LoadAsync(scope, alias, ContentViewPublicationState.Published, ct);
        if (published is null || !TryCreatePublicRequest(published, scope, callerParameters, take, out var request)) return null;
        return await ExecuteCachedAsync(published, scope, $"list:{request!.Take}", request, ct);
    }

    public Task<ContentViewExecutionResult?> ExecuteEntryAsync(ContentSurrealViewRevision view, ContentViewScope scope, string entryId, CancellationToken ct = default)
        => ExecuteVirtualAsync(view, scope, view.EntrySelectStatement, "$entryId", entryId, 1, ct);

    public Task<ContentViewExecutionResult?> SearchEntriesAsync(ContentSurrealViewRevision view, ContentViewScope scope, string search, int take, CancellationToken ct = default)
        => ExecuteVirtualAsync(view, scope, view.SearchSelectStatement, "$search", search, take, ct);

    private async Task<ContentViewExecutionResult?> ExecuteVirtualAsync(ContentSurrealViewRevision view, ContentViewScope scope,
        string? statement, string parameter, string value, int take, CancellationToken ct)
    {
        var invalidValue = value.Length > 256
            || string.Equals(parameter, "$entryId", StringComparison.Ordinal) && value.Length == 0;
        if (!view.IsPublished || view.Scope != scope || string.IsNullOrWhiteSpace(statement) || !executor.IsReadOnlyGuaranteed || take is <= 0 or > 100
            || invalidValue || !TryPrepareVirtualStatement(view, statement, parameter, take, out var executionStatement)
            || !TryBindSystemVirtualParameters(scope, parameter, value, out var bound)) return null;
        var limitedTake = take;
        var limits = new ContentViewExecutionLimits(limitedTake, limitedTake);
        if (!TryGetSafeStatement(executionStatement, limits, out var source)) return null;
        var virtualRevision = view with { SelectStatement = executionStatement };
        var path = parameter == "$entryId" ? "entry" : $"search:{limitedTake}";
        return await ExecuteCachedAsync(virtualRevision, scope, path,
            new ContentViewExecutionRequest(virtualRevision, scope, limitedTake, bound, limits, source!), ct);
    }

    private bool TryBindSystemVirtualParameters(ContentViewScope scope, string parameter, string value, out IReadOnlyDictionary<string, object?> bound)
    {
        bound = new Dictionary<string, object?>();
        if (!scopeBinder.TryBind(scope, new Dictionary<string, object?>(), out var scoped)) return false;
        bound = new Dictionary<string, object?>(scoped, StringComparer.Ordinal) { [parameter] = value };
        return true;
    }

    private async Task<ContentViewExecutionResult> ExecuteBoundedAsync(ContentViewExecutionRequest request, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(request.Limits.EffectiveTimeout);
        var result = await executor.ExecuteAsync(request, timeout.Token);
        var bounded = ContentViewExecutionLimitEnforcer.Enforce(result, request.Take, request.Limits);
        bounded = ProjectDeclaredSourceFields(bounded, request.Source);
        if (!shapeRegistry.TryGet(request.View.ShapeAlias, out var shape)
            || !ContentShapeRowValidator.TryValidateRows(bounded.Rows, shape!, out _))
            throw new InvalidOperationException("The read-only view executor returned rows that do not match the registered content shape.");
        return bounded;
    }

    private static ContentViewExecutionResult ProjectDeclaredSourceFields(ContentViewExecutionResult result, ContentViewSourceDefinition source)
    {
        if (source.OutputFieldMappings is not { Count: > 0 } map) return result;
        var projected = result.Rows.Select(row => (IReadOnlyDictionary<string, object?>)map
            .Where(mapping => row.TryGetValue(mapping.Key, out _))
            .ToDictionary(mapping => mapping.Value, mapping => row[mapping.Key], StringComparer.Ordinal)).ToArray();
        return new ContentViewExecutionResult(projected, result.IsTruncated);
    }

    internal bool ValidatePublishedEntryStatements(ContentSurrealViewRevision view, int maximumSearchTake)
    {
        if (string.IsNullOrWhiteSpace(view.EntrySelectStatement) || string.IsNullOrWhiteSpace(view.SearchSelectStatement)) return false;
        return IsSafeStatement(view.EntrySelectStatement, 1, out var entrySource)
            && TryGetPhysicalOutputField(entrySource!, view.IdentityField, out var identityField)
            && SurrealSelectValidator.HasRequiredBoundEquality(view.EntrySelectStatement, identityField!, "$entryId", classifier)
            && IsSafeStatement(view.SearchSelectStatement, maximumSearchTake, out _)
            && SurrealSelectValidator.HasRequiredBoundParameter(view.SearchSelectStatement, "$search", classifier);
    }

    private bool TryPrepareVirtualStatement(
        ContentSurrealViewRevision view,
        string statement,
        string parameter,
        int take,
        out string executionStatement)
    {
        executionStatement = string.Empty;
        if (string.Equals(parameter, "$entryId", StringComparison.Ordinal))
        {
            if (!string.Equals(statement, view.EntrySelectStatement, StringComparison.Ordinal)
                || !IsSafeStatement(statement, 1, out var source)
                || !TryGetPhysicalOutputField(source!, view.IdentityField, out var identityField)
                || !SurrealSelectValidator.HasRequiredBoundEquality(statement, identityField!, "$entryId", classifier)) return false;
            executionStatement = statement;
            return true;
        }

        if (!string.Equals(parameter, "$search", StringComparison.Ordinal)
            || !string.Equals(statement, view.SearchSelectStatement, StringComparison.Ordinal)
            || !IsSafeStatement(statement, 100, out _)
            || !SurrealSelectValidator.HasRequiredBoundParameter(statement, "$search", classifier)
            || classifier is not IContentViewRuntimeLimitRewriter rewriter
            || !rewriter.TryRewriteTerminalLimit(statement, take, out executionStatement)) return false;
        return true;
    }

    private static bool TryGetPhysicalOutputField(
        ContentViewSourceDefinition source,
        string shapeField,
        out string? physicalField)
    {
        physicalField = shapeField;
        if (source.OutputFieldMappings is not { Count: > 0 } mappings) return true;
        physicalField = mappings
            .Where(mapping => string.Equals(mapping.Value, shapeField, StringComparison.Ordinal))
            .Select(mapping => mapping.Key)
            .SingleOrDefault();
        return !string.IsNullOrWhiteSpace(physicalField);
    }

    private bool IsSafeStatement(string statement, int maximumTake, out ContentViewSourceDefinition? source)
        => TryGetSafeStatement(statement, new ContentViewExecutionLimits(maximumTake, maximumTake), out source);

    private bool TryGetSafeStatement(string statement, ContentViewExecutionLimits limits, out ContentViewSourceDefinition? source)
    {
        // Multi-source plans remain fail-closed until the relationship-backed plan factory emits
        // and verifies them from a locked schema definition.  Free-form registered plans never
        // execute publicly.
        return SurrealSelectValidator.TryGetSafeRegisteredSource(statement, classifier, limits, sourceRegistry, out source);
    }

    private static bool IsValidIdentityField(ContentShapeDefinition shape, string identityField)
        => identityField.Length is > 0 and <= 256
            && shape.Fields.Any(field => string.Equals(field.Name, identityField, StringComparison.Ordinal)
                && field.Required
                && field.Type == ContentShapeFieldType.String);

    private bool IsAdminReadOnlyStatement(string statement)
        => TryGetAdminReadOnlySource(statement, out _);

    private bool TryGetAdminReadOnlySource(string statement, out ContentViewSourceDefinition? source)
    {
        source = null;
        if (classifier is not IAdminReadOnlyStatementClassifier administratorClassifier
            || !administratorClassifier.IsSingleReadOnlySelect(statement)) return false;

        // The administrator transport is read-only, but it is not a row-security boundary.
        // Every preview source must therefore be registered and its exact tenant/site fields
        // must be the predicates bound by the server-owned scope binder.
        var classification = classifier.Classify(statement);
        return classification.SourceTable is { Length: > 0 } table
            && sourceRegistry is { IsValid: true }
            && sourceRegistry.TryGetByTable(table, out source)
            && classification.HasExactRootScopePredicates
            && string.Equals(classification.TenantField, source!.TenantField, StringComparison.Ordinal)
            && string.Equals(classification.SiteField, source.SiteField, StringComparison.Ordinal);
    }

    private bool TryCreatePublicRequest(ContentSurrealViewRevision view, ContentViewScope scope,
        IReadOnlyDictionary<string, object?> callerParameters, int take, out ContentViewExecutionRequest? request)
    {
        request = null;
        var limits = new ContentViewExecutionLimits();
        if (!view.IsPublished || view.Scope != scope || !scope.IsValid || !executor.IsReadOnlyGuaranteed
            || HasPublicRelationshipPlan(view)
            || take <= 0 || take > limits.MaximumTake || !TryGetSafeStatement(view.SelectStatement, limits, out var source)
            || !scopeBinder.TryBind(scope, callerParameters, out var bound)) return false;
        request = new ContentViewExecutionRequest(view, scope, take, bound, limits, source!);
        return true;
    }

    private bool HasVerifiedRelationshipPlan(ContentSurrealViewRevision view)
        => HasPublicRelationshipPlan(view)
           && relationshipPlans?.TryGet(view.PublicPlanAlias!, view.PublicPlanFingerprint!, out var plan) == true
           && string.Equals(plan!.PlanFingerprint, view.PublicPlanFingerprint, StringComparison.Ordinal)
           && relationshipDialect is { IsVerified: true }
           && string.Equals(relationshipDialect.Fingerprint, view.PublicPlanDialectFingerprint, StringComparison.Ordinal);

    private static bool HasPublicRelationshipPlan(ContentSurrealViewRevision view)
        => !string.IsNullOrWhiteSpace(view.PublicPlanAlias)
           || !string.IsNullOrWhiteSpace(view.PublicPlanFingerprint)
           || !string.IsNullOrWhiteSpace(view.PublicPlanDialectFingerprint);

    private async Task<ContentViewExecutionResult> ExecuteCachedAsync(ContentSurrealViewRevision view, ContentViewScope scope,
        string path, ContentViewExecutionRequest request, CancellationToken ct)
    {
        var localGeneration = await cacheGenerationProvider.GetGenerationAsync(scope, ct);
        var sharedGeneration = distributedCacheCoordinator is { IsDistributed: true }
            ? await distributedCacheCoordinator.GetGenerationAsync(scope, ct)
            : 0L;
        // Generation components have different authorities and may be random distributed tokens.
        // Keep them as canonical key identity instead of numerically combining them (which can
        // overflow and loses which invalidation boundary changed).
        var generationIdentity = $"{view.CacheGeneration}:{localGeneration}:{sharedGeneration}";
        var planIdentity = $"{view.PublicPlanFingerprint ?? "single-source"}:{view.PublicPlanDialectFingerprint ?? "default"}";
        var identityHash = HashIdentity(generationIdentity, planIdentity, HashParameters(request.Parameters));
        var cacheKey = ContentViewCacheKeys.Create(scope, $"{view.Alias}:{path}", view.Version, 0, identityHash);
        if (view.CacheEnabled && await executionCache.TryGetAsync(cacheKey, ct) is { } cached) return cached;
        var result = await ExecuteBoundedAsync(request, ct);
        // A missing exact entry is a not-found result, not a cacheable value.  Keeping it out of
        // the cache means a newly available record is visible immediately even before its TTL.
        if (view.CacheEnabled && (path != "entry" || result.Rows.Count > 0))
            await executionCache.SetAsync(cacheKey, result, view.CacheDuration ?? TimeSpan.FromMinutes(5), ct);
        return result;
    }

    private static string HashParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        var canonical = JsonSerializer.Serialize(parameters
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => Canonicalize(x.Value), StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16];
    }

    private static string HashIdentity(params string[] components)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', components))))[..16];

    private static object? Canonicalize(object? value) => value switch
    {
        null => null,
        IReadOnlyDictionary<string, object?> dictionary => dictionary.OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => Canonicalize(x.Value), StringComparer.Ordinal),
        IDictionary<string, object?> dictionary => dictionary.OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => Canonicalize(x.Value), StringComparer.Ordinal),
        System.Collections.IDictionary dictionary => dictionary.Keys.Cast<object?>()
            .Select(key => new KeyValuePair<string, object?>(Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, dictionary[key]))
            .OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => Canonicalize(x.Value), StringComparer.Ordinal),
        System.Collections.IEnumerable enumerable when value is not string => enumerable.Cast<object?>().Select(Canonicalize).ToArray(),
        _ => value
    };
}

public static class ContentViewExecutionLimitEnforcer
{
    public static ContentViewExecutionResult Enforce(ContentViewExecutionResult result, int take, ContentViewExecutionLimits limits)
    {
        if (result.Rows.Count > take || result.Rows.Count > limits.MaximumRows)
            throw new InvalidOperationException("The read-only view executor exceeded the configured row limit.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(result.Rows);
        if (bytes.Length > limits.MaximumBytes)
            throw new InvalidOperationException("The read-only view executor exceeded the configured byte limit.");
        using var document = JsonDocument.Parse(bytes);
        if (GetDepth(document.RootElement, 1) > limits.MaximumDepth)
            throw new InvalidOperationException("The read-only view executor exceeded the configured value-depth limit.");
        return result;
    }

    private static int GetDepth(JsonElement element, int depth) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Select(property => GetDepth(property.Value, depth + 1)).Prepend(depth).Max(),
        JsonValueKind.Array => element.EnumerateArray().Select(value => GetDepth(value, depth + 1)).Prepend(depth).Max(),
        _ => depth
    };
}

public interface IContentSurrealViewService
{
    Task<ContentSurrealViewRevision?> LoadPublishedAsync(ContentViewScope scope, string alias, CancellationToken ct = default);
    Task<ContentSurrealViewRevision?> LoadDraftAsync(ContentViewScope scope, string alias, CancellationToken ct = default);
    Task<ContentSurrealViewRevision?> SaveDraftAsync(ContentSurrealViewRevision draft, int maximumPreviewTake, CancellationToken ct = default);
    Task<ContentSurrealViewRevision?> PublishAsync(ContentViewScope scope, string alias, long draftVersion, CancellationToken ct = default);
    Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default);
    Task<ContentViewExecutionResult?> PreviewAsync(ContentSurrealViewRevision draft, ContentViewScope scope, IReadOnlyDictionary<string, object?> callerParameters, int take, int maximumPreviewTake, CancellationToken ct = default);
    Task<ContentViewExecutionResult?> ExecutePublicAsync(ContentViewScope scope, string alias, IReadOnlyDictionary<string, object?> callerParameters, int take, CancellationToken ct = default);
    Task<ContentViewExecutionResult?> ExecuteEntryAsync(ContentSurrealViewRevision view, ContentViewScope scope, string entryId, CancellationToken ct = default);
    Task<ContentViewExecutionResult?> SearchEntriesAsync(ContentSurrealViewRevision view, ContentViewScope scope, string search, int take, CancellationToken ct = default);
}
