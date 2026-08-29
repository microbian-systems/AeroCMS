namespace Aero.Cms.Abstractions.Content.Views;

/// <summary>Stable identity for an entry exposed by a content view.</summary>
public readonly record struct ContentEntryKey(string Provider, string StableId)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Provider) && !string.IsNullOrWhiteSpace(StableId);
}

/// <summary>A provider-qualified entry returned by either persisted or query-backed content.</summary>
public sealed record ContentEntry(ContentEntryKey Key, ContentViewScope Scope, IReadOnlyDictionary<string, object?> Values);

/// <summary>Site-scoped source boundary that lets virtual entries be selected where ordinary content is selected.</summary>
public interface IContentEntrySourceProvider
{
    string Provider { get; }
    Task<ContentEntry?> FindAsync(ContentViewScope scope, string stableId, CancellationToken ct = default);
    /// <summary>Bounded, site-scoped selector lookup. A missing key from <see cref="FindAsync"/> is a not-found result.</summary>
    Task<IReadOnlyList<ContentEntry>> SearchAsync(ContentViewScope scope, string? culture, string? query, int take, CancellationToken ct = default);
}

/// <summary>Describes where a virtual content entry originated without coupling callers to storage.</summary>
public sealed record ContentEntrySource(
    ContentEntrySourceKind Kind,
    string StableSourceId,
    string? SourceVersion = null)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(StableSourceId);
}

public enum ContentEntrySourceKind { ContentItem, External, Derived }

/// <summary>A code-owned, immutable shape that view rows must project.</summary>
public sealed record ContentShapeDefinition(
    string Alias,
    IReadOnlyList<ContentShapeField> Fields,
    string SchemaFingerprint)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Alias)
        && !string.IsNullOrWhiteSpace(SchemaFingerprint)
        && Fields is { Count: > 0 }
        && Fields.All(shapeField => !string.IsNullOrWhiteSpace(shapeField.Name) && shapeField.IsValid);
}

/// <summary>Compile-time registration point for a code-owned content shape.</summary>
public interface IContentShape
{
    ContentShapeDefinition Definition { get; }
}

public interface IContentShapeRegistry
{
    bool IsValid { get; }
    bool TryGet(string alias, out ContentShapeDefinition? definition);
    IReadOnlyList<string> Errors { get; }
    IReadOnlyList<ContentShapeDefinition> Definitions { get; }
}

/// <summary>Validates shape registrations once; duplicate aliases and fingerprints fail closed.</summary>
public sealed class ContentShapeRegistry : IContentShapeRegistry
{
    private readonly Dictionary<string, ContentShapeDefinition> definitions = new(StringComparer.Ordinal);
    public List<string> ValidationErrors { get; } = [];
    public bool IsValid => ValidationErrors.Count == 0;
    public IReadOnlyList<string> Errors => ValidationErrors;
    public IReadOnlyList<ContentShapeDefinition> Definitions => definitions.Values.OrderBy(x => x.Alias, StringComparer.Ordinal).ToArray();

    public ContentShapeRegistry(IEnumerable<IContentShape> shapes)
    {
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        var registrations = shapes.Select(shape => shape.Definition).ToArray();
        var aliases = registrations.Where(definition => !string.IsNullOrWhiteSpace(definition.Alias))
            .Select(definition => definition.Alias).ToHashSet(StringComparer.Ordinal);
        foreach (var definition in registrations)
        {
            if (!definition.IsValid)
            {
                ValidationErrors.Add($"Shape '{definition.Alias}' is invalid.");
                continue;
            }
            var canonicalFingerprint = ContentShapeFingerprint.Create(definition);
            if (!string.Equals(canonicalFingerprint, definition.SchemaFingerprint, StringComparison.Ordinal))
            {
                ValidationErrors.Add($"Shape '{definition.Alias}' has a fingerprint that does not match its canonical recursive structure.");
                continue;
            }
            var shapeErrors = ContentShapeDefinitionValidator.Validate(definition, aliases);
            if (shapeErrors.Count > 0)
            {
                ValidationErrors.AddRange(shapeErrors);
                continue;
            }
            if (!definitions.TryAdd(definition.Alias, definition))
                ValidationErrors.Add($"Duplicate shape alias '{definition.Alias}'.");
            if (!fingerprints.Add(definition.SchemaFingerprint))
                ValidationErrors.Add($"Duplicate shape fingerprint '{definition.SchemaFingerprint}'.");
        }
    }

    public bool TryGet(string alias, out ContentShapeDefinition? definition)
        => definitions.TryGetValue(alias, out definition) && IsValid;
}

/// <summary>Creates a deterministic fingerprint from the complete recursive, code-owned shape.</summary>
public static class ContentShapeFingerprint
{
    public static string Create(ContentShapeDefinition definition)
    {
        var canonical = CanonicalizeDefinition(definition);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
    }

    private static string CanonicalizeDefinition(ContentShapeDefinition definition)
        => $"shape:{definition.Alias}|fields:[{string.Join(',', definition.Fields.Select(CanonicalizeField))}]";

    private static string CanonicalizeField(ContentShapeField field)
        => $"{field.Name}:{field.Type}:{field.Required}:{field.ReferenceShapeAlias ?? string.Empty}"
            + $"{{{string.Join(',', (field.Fields ?? []).Select(CanonicalizeField))}}}"
            + (field.Item is null ? string.Empty : $"[{CanonicalizeField(field.Item)}]");
}

public static class ContentShapeDefinitionValidator
{
    public static IReadOnlyList<string> Validate(ContentShapeDefinition definition, IEnumerable<string> knownAliases)
    {
        var errors = new List<string>();
        ValidateFields(definition.Alias, definition.Fields, knownAliases.ToHashSet(StringComparer.Ordinal), errors);
        return errors;
    }

    private static void ValidateFields(string shapeAlias, IReadOnlyList<ContentShapeField> fields, HashSet<string> knownAliases, List<string> errors)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!names.Add(field.Name)) errors.Add($"Shape '{shapeAlias}' has duplicate field '{field.Name}'.");
            if (field.Type == ContentShapeFieldType.Object && field.Fields is not null)
                ValidateFields(shapeAlias, field.Fields, knownAliases, errors);
            if (field.Type == ContentShapeFieldType.List && field.Item is not null)
                ValidateFields(shapeAlias, [field.Item], knownAliases, errors);
            if (field.Type == ContentShapeFieldType.Reference
                && (field.ReferenceShapeAlias is null || !knownAliases.Contains(field.ReferenceShapeAlias)))
                errors.Add($"Shape '{shapeAlias}' references unresolved shape '{field.ReferenceShapeAlias ?? string.Empty}'.");
        }
    }
}

/// <summary>Recursive descriptor for scalar, object, list, and reference values.</summary>
public sealed record ContentShapeField(
    string Name,
    ContentShapeFieldType Type,
    bool Required = false,
    IReadOnlyList<ContentShapeField>? Fields = null,
    ContentShapeField? Item = null,
    string? ReferenceShapeAlias = null)
{
    public bool IsValid => Type switch
    {
        ContentShapeFieldType.Object => Fields is { Count: > 0 } && Fields.All(nested => nested.IsValid),
        ContentShapeFieldType.List => Item is not null && Item.IsValid,
        ContentShapeFieldType.Reference => !string.IsNullOrWhiteSpace(ReferenceShapeAlias),
        _ => true
    };
}

public enum ContentShapeFieldType { String, Number, Boolean, DateTime, Json, ContentEntryKey, ContentEntrySource, Object, List, Reference }

/// <summary>Server-owned scope. It must be resolved from authorization, never request input.</summary>
public readonly record struct ContentViewScope(long TenantId, long SiteId)
{
    public bool IsValid => TenantId > 0 && SiteId > 0;
}

public enum ContentViewPublicationState { Draft, Published }

/// <summary>An immutable revision of a site-owned Surreal view definition.</summary>
public sealed record ContentSurrealViewRevision(
    long Id,
    ContentViewScope Scope,
    string Alias,
    string ShapeAlias,
    string ShapeFingerprint,
    string SelectStatement,
    string IdentityField,
    string? TitleField,
    long Version,
    ContentViewPublicationState PublicationState,
    DateTimeOffset CreatedOn,
    string? CreatedBy = null,
    bool CacheEnabled = true,
    TimeSpan? CacheDuration = null,
    long CacheGeneration = 0,
    string? EntrySelectStatement = null,
    string? SearchSelectStatement = null,
    long? RelationshipId = null,
    string? RelationshipSchemaFingerprint = null,
    bool PublicExecutionEligible = false,
    string? PublicExecutionIneligibilityReason = null,
    string? PublicPlanAlias = null,
    string? PublicPlanFingerprint = null,
    string? PublicPlanDialectFingerprint = null,
    string? SourceAlias = null,
    string? SourceSchemaFingerprint = null)
{
    public bool IsPublished => PublicationState == ContentViewPublicationState.Published;
    public bool HasEntryIdentity => !string.IsNullOrWhiteSpace(IdentityField);
}

/// <summary>Bounded request passed only to a dedicated read-only view executor.</summary>
public sealed record ContentViewExecutionLimits(
    int MaximumTake = 100,
    int MaximumRows = 100,
    int MaximumBytes = 1_048_576,
    int MaximumDepth = 16,
    TimeSpan? Timeout = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(15);
    public bool IsValid => MaximumTake > 0 && MaximumRows > 0 && MaximumRows <= MaximumTake
        && MaximumBytes > 0 && MaximumDepth > 0;
}

public sealed record ContentViewExecutionRequest(
    ContentSurrealViewRevision View,
    ContentViewScope Scope,
    int Take,
    IReadOnlyDictionary<string, object?> Parameters,
    ContentViewExecutionLimits Limits,
    ContentViewSourceDefinition Source,
    ContentViewExecutionPlanKind PlanKind = ContentViewExecutionPlanKind.SingleSource);

public enum ContentViewExecutionPlanKind { SingleSource, GeneratedRelationship }

/// <summary>Reports whether a generated relationship dialect has passed the pinned runtime proof.</summary>
public interface IContentViewRelationshipPlanDialectCapability
{
    bool IsVerified { get; }
    string Fingerprint { get; }
}

/// <summary>Default deny-all capability. Graph public reads remain unavailable without an exact runtime proof.</summary>
public sealed class DisabledContentViewRelationshipPlanDialectCapability : IContentViewRelationshipPlanDialectCapability
{
    public bool IsVerified => false;
    public string Fingerprint => "unverified";
}

/// <summary>
/// Code-owned authorization for one physical SurrealDB table exposed to content views.  The
/// source is deliberately single-table: joins, graph traversals, and arbitrary roots fail
/// closed until a host provides a separate descriptor that can prove their complete scope.
/// </summary>
public enum ContentViewSourceKind
{
    Table = 0,
    MaterializedView = 1
}

public sealed record ContentViewSourceDefinition(
    string Alias,
    string Table,
    string TenantField = "tenant_id",
    string SiteField = "site_id",
    IReadOnlyDictionary<string, string>? OutputFieldMappings = null,
    IReadOnlyList<ContentViewRequiredBooleanPredicate>? RequiredBooleanPredicates = null,
    ContentViewSourceKind Kind = ContentViewSourceKind.Table,
    string? DisplayName = null,
    string? Description = null,
    string? SuggestedShapeAlias = null,
    string? IdentityField = null,
    string? TitleField = null,
    string? SearchField = null)
{
    public bool IsValid => IsIdentifier(Alias) && IsIdentifier(Table)
        && IsIdentifier(TenantField) && IsIdentifier(SiteField)
        && (OutputFieldMappings is null || OutputFieldMappings.All(mapping => IsIdentifier(mapping.Key) && IsIdentifier(mapping.Value))
            && OutputFieldMappings.Values.Distinct(StringComparer.Ordinal).Count() == OutputFieldMappings.Count)
        && (RequiredBooleanPredicates is null
            || RequiredBooleanPredicates.All(predicate => IsIdentifier(predicate.Field))
            && RequiredBooleanPredicates.Select(predicate => predicate.Field).Distinct(StringComparer.Ordinal).Count() == RequiredBooleanPredicates.Count)
        && IsOptionalAlias(SuggestedShapeAlias)
        && IsOptionalIdentifier(IdentityField)
        && IsOptionalIdentifier(TitleField)
        && IsOptionalIdentifier(SearchField)
        && (IdentityField is null || TryGetPhysicalField(IdentityField, out _))
        && (TitleField is null || TryGetPhysicalField(TitleField, out _))
        && (SearchField is null || TryGetPhysicalField(SearchField, out _));

    public string EffectiveDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? Alias : DisplayName.Trim();

    public bool TryGetPhysicalField(string logicalField, out string physicalField)
    {
        physicalField = string.Empty;
        if (!IsIdentifier(logicalField)) return false;
        if (OutputFieldMappings is null)
        {
            physicalField = logicalField;
            return true;
        }

        var mapping = OutputFieldMappings.FirstOrDefault(item => string.Equals(item.Value, logicalField, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(mapping.Key)) return false;
        physicalField = mapping.Key;
        return true;
    }

    private static bool IsOptionalIdentifier(string? value) => value is null || IsIdentifier(value);
    private static bool IsOptionalAlias(string? value)
        => value is null || (!string.IsNullOrWhiteSpace(value)
            && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-'));

    private static bool IsIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && (char.IsLetter(value[0]) || value[0] == '_')
           && value.All(character => char.IsLetterOrDigit(character) || character == '_');
}

/// <summary>
/// A code-owned boolean equality that every query against a registered source must contain.
/// This supports generic publication/current-row flags without trusting an editor to remember
/// a data-visibility predicate.
/// </summary>
public sealed record ContentViewRequiredBooleanPredicate(string Field, bool Value);

/// <summary>Compile-time registration point for queryable data sources.</summary>
public interface IContentViewSource
{
    ContentViewSourceDefinition Definition { get; }
}

public interface IContentViewSourceRegistry
{
    bool IsValid { get; }
    bool HasSources { get; }
    IReadOnlyList<string> Errors { get; }
    IReadOnlyList<ContentViewSourceDefinition> Definitions => [];
    bool TryGetByAlias(string alias, out ContentViewSourceDefinition? source)
    {
        source = null;
        return false;
    }
    bool TryGetByTable(string table, out ContentViewSourceDefinition? source);
}

/// <summary>
/// Server-observed, code-registered source contract with canonical bounded statements.
/// It is safe to persist as a Content View binding but does not grant schema ownership.
/// </summary>
public sealed record ContentViewSourceSnapshot(
    string Alias,
    string DisplayName,
    string? Description,
    ContentViewSourceKind Kind,
    string Table,
    string SchemaFingerprint,
    string? SuggestedShapeAlias,
    string IdentityField,
    string? TitleField,
    string ListSelectStatement,
    string EntrySelectStatement,
    string SearchSelectStatement);

/// <summary>
/// Resolves only host-registered physical sources after observing their current schema.
/// Implementations fail closed when schema metadata or the source registry is unavailable.
/// </summary>
public interface IContentViewSourceSnapshotService
{
    Task<IReadOnlyList<ContentViewSourceSnapshot>> ListAsync(CancellationToken ct = default);
    Task<ContentViewSourceSnapshot?> GetAsync(string alias, CancellationToken ct = default);
}

/// <summary>
/// A code-owned, exact query plan for an approved multi-source read.  Editors may reference its
/// statement, but cannot manufacture one: every table and graph edge is registered with the
/// tenant/site fields that must be constrained by the plan.
/// </summary>
public sealed record ContentViewTrustedQueryPlanDefinition(
    string Alias,
    string SelectStatement,
    ContentViewSourceDefinition RootSource,
    IReadOnlyList<ContentViewSourceDefinition> RelatedSources,
    IReadOnlyList<ContentViewSourceDefinition> EdgeSources,
    IReadOnlyList<ContentViewScopedPlanSource>? ScopedDescriptors = null)
{
    private IReadOnlyList<ContentViewScopedPlanSource> RequiredDescriptors => ScopedDescriptors ?? [];
    public bool IsValid => !string.IsNullOrWhiteSpace(Alias)
        && !string.IsNullOrWhiteSpace(SelectStatement)
        && RootSource.IsValid
        && RelatedSources.All(source => source.IsValid)
        && EdgeSources.All(source => source.IsValid)
        && RelatedSources.All(source => !string.Equals(source.Table, RootSource.Table, StringComparison.OrdinalIgnoreCase))
        && EdgeSources.All(source => !string.Equals(source.Table, RootSource.Table, StringComparison.OrdinalIgnoreCase))
        && RequiredDescriptors.Count == ScopedSources.Count
        && ScopedSources.All(source => RequiredDescriptors.Any(descriptor => string.Equals(descriptor.Source.Table, source.Table, StringComparison.OrdinalIgnoreCase)))
        && RequiredDescriptors.All(descriptor => descriptor.IsValid && descriptor.HasRequiredScopePredicates(SelectStatement));

    public IReadOnlyList<ContentViewSourceDefinition> ScopedSources
        => [RootSource, .. RelatedSources, .. EdgeSources];

    /// <summary>Stable identity derived from the typed, registered plan rather than editor SQL.</summary>
    public string PlanFingerprint => ContentViewPublicPlanFingerprint.Create(this);
}

/// <summary>Canonicalizes an exact code-owned relationship plan for immutable publication binding.</summary>
public static class ContentViewPublicPlanFingerprint
{
    public static string Create(ContentViewTrustedQueryPlanDefinition plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var sources = plan.ScopedSources.Select(CanonicalizeSource).OrderBy(value => value, StringComparer.Ordinal);
        var descriptors = (plan.ScopedDescriptors ?? [])
            .Select(descriptor => $"{descriptor.Qualifier}:{CanonicalizeSource(descriptor.Source)}")
            .OrderBy(value => value, StringComparer.Ordinal);
        var canonical = $"alias:{plan.Alias}|sql:{plan.SelectStatement}|sources:[{string.Join(',', sources)}]|scope:[{string.Join(',', descriptors)}]";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)))[..32];
    }

    private static string CanonicalizeSource(ContentViewSourceDefinition source)
    {
        var mappings = string.Join(',', (source.OutputFieldMappings ?? new Dictionary<string, string>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}>{item.Value}"));
        var requiredPredicates = string.Join(',', (source.RequiredBooleanPredicates ?? [])
            .OrderBy(item => item.Field, StringComparer.Ordinal)
            .Select(item => $"{item.Field}={item.Value.ToString().ToLowerInvariant()}"));
        return $"{source.Alias}:{source.Table}:{source.TenantField}:{source.SiteField}:{mappings}:{requiredPredicates}";
    }
}

/// <summary>Server-verifiable scope binding for one source or relationship edge in a trusted plan.</summary>
public sealed record ContentViewScopedPlanSource(ContentViewSourceDefinition Source, string Qualifier)
{
    public bool IsValid => Source.IsValid && !string.IsNullOrWhiteSpace(Qualifier)
        && Qualifier.All(character => char.IsLetterOrDigit(character) || character == '_');

    public bool HasRequiredScopePredicates(string statement)
        => statement.Contains($"{Qualifier}.{Source.TenantField} = {ReservedContentViewScopeBinder.TenantParameter}", StringComparison.Ordinal)
            && statement.Contains($"{Qualifier}.{Source.SiteField} = {ReservedContentViewScopeBinder.SiteParameter}", StringComparison.Ordinal);
}

/// <summary>Host registrations for exact, reviewed relationship/graph query plans.</summary>
public interface IContentViewTrustedQueryPlan
{
    ContentViewTrustedQueryPlanDefinition Definition { get; }
}

public interface IContentViewTrustedQueryPlanRegistry
{
    bool IsValid { get; }
    bool TryGet(string alias, string fingerprint, out ContentViewTrustedQueryPlanDefinition? plan);
}

/// <summary>Fail-closed registry for hosts that do not opt into reviewed multi-source plans.</summary>
public sealed class EmptyContentViewTrustedQueryPlanRegistry : IContentViewTrustedQueryPlanRegistry
{
    public bool IsValid => true;
    public bool TryGet(string alias, string fingerprint, out ContentViewTrustedQueryPlanDefinition? plan)
    { plan = null; return false; }
}

public sealed class ContentViewTrustedQueryPlanRegistry : IContentViewTrustedQueryPlanRegistry
{
    private readonly Dictionary<string, ContentViewTrustedQueryPlanDefinition> byIdentity = new(StringComparer.Ordinal);
    public bool IsValid { get; }

    public ContentViewTrustedQueryPlanRegistry(IEnumerable<IContentViewTrustedQueryPlan> plans)
    {
        IsValid = true;
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plan in plans.Select(item => item.Definition))
        {
            if (!plan.IsValid || !aliases.Add(plan.Alias)
                || !byIdentity.TryAdd($"{plan.Alias}:{plan.PlanFingerprint}", plan))
            {
                IsValid = false;
                return;
            }
        }
    }

    public bool TryGet(string alias, string fingerprint, out ContentViewTrustedQueryPlanDefinition? plan)
    {
        plan = null;
        return IsValid && byIdentity.TryGetValue($"{alias}:{fingerprint}", out plan);
    }
}

public sealed class ContentViewSourceRegistry : IContentViewSourceRegistry
{
    private readonly Dictionary<string, ContentViewSourceDefinition> byTable = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ContentViewSourceDefinition> byAlias = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ValidationErrors { get; } = [];
    public bool IsValid => ValidationErrors.Count == 0;
    public bool HasSources => byTable.Count > 0;
    public IReadOnlyList<string> Errors => ValidationErrors;
    public IReadOnlyList<ContentViewSourceDefinition> Definitions => IsValid
        ? byAlias.Values.OrderBy(source => source.EffectiveDisplayName, StringComparer.OrdinalIgnoreCase).ToArray()
        : [];

    public ContentViewSourceRegistry(IEnumerable<IContentViewSource> sources)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources.Select(source => source.Definition))
        {
            if (!source.IsValid)
            {
                ValidationErrors.Add($"Content view source '{source.Alias}' is invalid.");
                continue;
            }
            if (!aliases.Add(source.Alias) || !byAlias.TryAdd(source.Alias, source)) ValidationErrors.Add($"Duplicate content view source alias '{source.Alias}'.");
            if (!byTable.TryAdd(source.Table, source)) ValidationErrors.Add($"Duplicate content view source table '{source.Table}'.");
        }
    }

    public bool TryGetByAlias(string alias, out ContentViewSourceDefinition? source)
    {
        source = null;
        return IsValid && byAlias.TryGetValue(alias, out source);
    }

    public bool TryGetByTable(string table, out ContentViewSourceDefinition? source)
    {
        source = null;
        return IsValid && byTable.TryGetValue(table, out source);
    }
}

/// <summary>Creates execution parameters from server-resolved scope. Callers may not supply reserved names.</summary>
public interface IContentViewScopeBinder
{
    bool TryBind(ContentViewScope scope, IReadOnlyDictionary<string, object?> callerParameters, out IReadOnlyDictionary<string, object?> parameters);
}

public sealed class ReservedContentViewScopeBinder : IContentViewScopeBinder
{
    public const string TenantParameter = "$tenantId";
    public const string SiteParameter = "$siteId";

    public bool TryBind(ContentViewScope scope, IReadOnlyDictionary<string, object?> callerParameters, out IReadOnlyDictionary<string, object?> parameters)
    {
        parameters = new Dictionary<string, object?>();
        if (!scope.IsValid || callerParameters.Keys.Any(IsReserved)) return false;

        var bound = new Dictionary<string, object?>(callerParameters, StringComparer.Ordinal)
        {
            [TenantParameter] = scope.TenantId,
            [SiteParameter] = scope.SiteId
        };
        parameters = bound;
        return true;
    }

    private static bool IsReserved(string parameter)
    {
        var normalized = parameter.StartsWith('$') ? parameter[1..] : parameter;
        return string.Equals(normalized, TenantParameter[1..], StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, SiteParameter[1..], StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "entryId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "search", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ContentViewExecutionResult(IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows, bool IsTruncated);

/// <summary>Durable store boundary for view draft and publication revisions.</summary>
public interface IContentSurrealViewStore
{
    Task<ContentSurrealViewRevision?> LoadAsync(ContentViewScope scope, string alias, ContentViewPublicationState state, CancellationToken ct = default);
    Task<IReadOnlyList<ContentSurrealViewRevision>> ListPublishedAsync(ContentViewScope scope, CancellationToken ct = default);
    Task<ContentSurrealViewRevision> SaveDraftAsync(ContentSurrealViewRevision draft, CancellationToken ct = default);
    Task<ContentSurrealViewRevision?> PublishAsync(ContentViewScope scope, string alias, long draftVersion, CancellationToken ct = default);
}

/// <summary>Cache invalidation boundary. Implementations invalidate by the tenant/site tag after a changed revision.</summary>
public interface IContentViewCacheInvalidator
{
    Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default);
}

/// <summary>Host-owned bridge that evicts rendered anonymous pages which consumed virtual views.</summary>
public interface IContentViewOutputCacheInvalidator
{
    Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default);
}

public sealed class DisabledContentViewOutputCacheInvalidator : IContentViewOutputCacheInvalidator
{
    public Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default) => Task.CompletedTask;
}

public static class ContentViewOutputCacheTags
{
    public static string Site(ContentViewScope scope) => $"content-view-site:{scope.TenantId}:{scope.SiteId}";
    public static string Provider(ContentViewScope scope, string provider) => $"{Site(scope)}:{provider.Trim().ToLowerInvariant()}";
}

/// <summary>
/// Optional cache-generation source for hosts whose cache implementation cannot remove every
/// distributed entry immediately.  A successful invalidation advances the site generation.
/// </summary>
public interface IContentViewCacheGenerationProvider
{
    Task<long> GetGenerationAsync(ContentViewScope scope, CancellationToken ct = default);
}

/// <summary>
/// Optional host-owned, shared generation authority for a content-view cache.  A host that runs
/// more than one application instance must provide an implementation backed by shared storage
/// (and may fan out local eviction separately).  The default is explicitly unavailable rather
/// than pretending that the in-memory cache crosses process boundaries.
/// </summary>
public interface IContentViewDistributedCacheCoordinator
{
    bool IsDistributed { get; }
    Task<long> GetGenerationAsync(ContentViewScope scope, CancellationToken ct = default);
    Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default);
}

/// <summary>Safe default for single-process hosts that have not configured shared cache state.</summary>
public sealed class DisabledContentViewDistributedCacheCoordinator : IContentViewDistributedCacheCoordinator
{
    public bool IsDistributed => false;
    public Task<long> GetGenerationAsync(ContentViewScope scope, CancellationToken ct = default) => Task.FromResult(0L);
    public Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Result cache is intentionally separate from content mutation caches.</summary>
public interface IContentViewExecutionCache
{
    Task<ContentViewExecutionResult?> TryGetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, ContentViewExecutionResult result, TimeSpan duration, CancellationToken ct = default);
    Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default);
}

public static class ContentViewCacheKeys
{
    public static string ScopePrefix(ContentViewScope scope) => $"content-view:{scope.TenantId}:{scope.SiteId}:";
    public static string Create(ContentViewScope scope, string alias, long version, long generation, string parameterHash)
        => $"{ScopePrefix(scope)}{alias}:{version}:{generation}:{parameterHash}";
    public static string Revision(ContentViewScope scope, string viewAlias, long version)
        => $"{ScopePrefix(scope)}{viewAlias}:{version}";
    public static string SiteTag(ContentViewScope scope) => $"content-view-site:{scope.TenantId}:{scope.SiteId}";
}

/// <summary>
/// Dedicated executor boundary. Implementations must not share a mutation-capable connection and
/// must enforce <see cref="ContentViewExecutionRequest.Limits"/> at the database/streaming boundary
/// (including cancellation, row count, and response bytes) before materializing an unbounded result.
/// The service independently rejects any noncompliant result as defense in depth.
/// </summary>
public interface IReadOnlyContentViewExecutor
{
    bool IsReadOnlyGuaranteed { get; }
    Task<ContentViewExecutionResult> ExecuteAsync(ContentViewExecutionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Host-owned transport boundary for query-backed public content.  Implementations must apply
/// row, byte, depth, and cancellation limits before materializing a response.  The Sable public
/// API currently has no such operation, so registering credentials alone is intentionally not
/// enough to activate public execution.
/// </summary>
public interface IContentViewBoundedQueryTransport
{
    bool EnforcesLimitsBeforeMaterialization { get; }
    Task<ContentViewExecutionResult> ExecuteBoundedAsync(ContentViewExecutionRequest request, CancellationToken ct = default);
}

/// <summary>Default fail-closed executor. Hosts must explicitly register a separate read-only database identity.</summary>
public sealed class DisabledContentViewExecutor : IReadOnlyContentViewExecutor
{
    public bool IsReadOnlyGuaranteed => false;
    public Task<ContentViewExecutionResult> ExecuteAsync(ContentViewExecutionRequest request, CancellationToken ct = default)
        => Task.FromException<ContentViewExecutionResult>(new InvalidOperationException("A separately configured read-only view executor is required."));
}

/// <summary>Separate administrator-only read boundary. It must use a dedicated SELECT-only
/// identity and enforce the supplied limits before response materialization.</summary>
public interface IAdminReadOnlyContentViewExecutor
{
    bool IsReadOnlyGuaranteed { get; }
    Task<ContentViewExecutionResult> ExecuteAsync(ContentSurrealViewRevision view, ContentViewScope scope,
        IReadOnlyDictionary<string, object?> parameters, ContentViewExecutionLimits limits, CancellationToken ct = default);
}

public sealed class DisabledAdminContentViewExecutor : IAdminReadOnlyContentViewExecutor
{
    public bool IsReadOnlyGuaranteed => false;
    public Task<ContentViewExecutionResult> ExecuteAsync(ContentSurrealViewRevision view, ContentViewScope scope,
        IReadOnlyDictionary<string, object?> parameters, ContentViewExecutionLimits limits, CancellationToken ct = default)
        => Task.FromException<ContentViewExecutionResult>(new InvalidOperationException("A separately configured administrator read-only view executor is required."));
}

/// <summary>Structural SurrealQL parser/classifier boundary. No classifier means no execution.</summary>
public interface IContentViewStatementClassifier
{
    ContentViewStatementClassification Classify(string statement);
}

/// <summary>
/// Administrator-preview-only lexical boundary. This intentionally does not establish public
/// execution safety; it only proves a single SELECT statement is being sent to a separately
/// configured read-only administrator transport.
/// </summary>
public interface IAdminReadOnlyStatementClassifier
{
    bool IsSingleReadOnlySelect(string statement);
}

/// <summary>
/// Proves that system-owned virtual-entry parameters occur in executable predicates,
/// rather than only in projections, comments, or string literals.
/// </summary>
public interface IContentViewBoundParameterClassifier
{
    bool HasBoundEquality(string statement, string field, string parameter);
    bool HasBoundPredicateParameter(string statement, string parameter);
}

/// <summary>Proves code-owned boolean source predicates occur in the executable WHERE clause.</summary>
public interface IContentViewRequiredPredicateClassifier
{
    bool HasBoundBooleanEquality(string statement, string field, bool value);
}

/// <summary>
/// Replaces only the already-classified terminal numeric LIMIT with a server-owned bounded value.
/// Public callers never provide SQL text or the replacement value directly.
/// </summary>
public interface IContentViewRuntimeLimitRewriter
{
    bool TryRewriteTerminalLimit(string statement, int requestedTake, out string rewrittenStatement);
}

public sealed record ContentViewStatementClassification(
    bool IsSingleReadOnlySelect,
    bool HasMutation,
    bool HasMultipleStatements,
    bool HasRequiredScopePredicates = false,
    int? Limit = null,
    string? SourceTable = null,
    bool HasExactRootScopePredicates = false,
    string? TenantField = null,
    string? SiteField = null);

public static class ContentViewExecutionPolicy
{
    public static bool CanExecutePublicly(
        ContentSurrealViewRevision view,
        ContentViewScope scope,
        IReadOnlyContentViewExecutor? executor,
        IContentViewStatementClassifier? classifier,
        IContentViewScopeBinder? scopeBinder,
        IReadOnlyDictionary<string, object?> callerParameters,
        int take,
        ContentViewExecutionLimits? limits,
        IContentViewSourceRegistry? sourceRegistry,
        out ContentViewExecutionRequest? request)
    {
        request = null;
        ContentViewSourceDefinition? source = null;
        if (!view.IsPublished
            || view.Scope != scope
            || !scope.IsValid
            || executor is not { IsReadOnlyGuaranteed: true }
            || limits is not { IsValid: true }
            || take > limits.MaximumTake
            || !SurrealSelectValidator.TryGetSafeRegisteredSource(view.SelectStatement, classifier, limits, sourceRegistry, out source)
            || scopeBinder is null
            || take <= 0
            || !scopeBinder.TryBind(scope, callerParameters, out var boundParameters)) return false;

        request = new ContentViewExecutionRequest(view, scope, take, boundParameters, limits, source!);
        return true;
    }
}

/// <summary>Lexical defense-in-depth around a required structural statement classifier.</summary>
public static class SurrealSelectValidator
{
    public static bool IsSafeForPreview(string statement, int maximumTake, IContentViewStatementClassifier? classifier)
        => IsSafeForExecution(statement, classifier, new ContentViewExecutionLimits(maximumTake, maximumTake));

    public static bool IsSafeForExecution(string statement, IContentViewStatementClassifier? classifier)
        => IsSafeForExecution(statement, classifier, new ContentViewExecutionLimits());

    public static bool IsSafeForExecution(string statement, IContentViewStatementClassifier? classifier, ContentViewExecutionLimits limits)
    {
        var classification = classifier?.Classify(statement);
        return limits.IsValid && classification is { IsSingleReadOnlySelect: true, HasMutation: false, HasMultipleStatements: false, HasRequiredScopePredicates: true }
            && classification.Limit is > 0 and <= 10000 && classification.Limit <= limits.MaximumTake;
    }

    public static bool TryGetSafeRegisteredSource(
        string statement,
        IContentViewStatementClassifier? classifier,
        ContentViewExecutionLimits limits,
        IContentViewSourceRegistry? sourceRegistry,
        out ContentViewSourceDefinition? source)
    {
        source = null;
        var classification = classifier?.Classify(statement);
        return sourceRegistry is { IsValid: true }
            && IsSafeForExecution(statement, classifier, limits)
            && classification?.SourceTable is { Length: > 0 } table
            && sourceRegistry.TryGetByTable(table, out source)
            && classification.HasExactRootScopePredicates
            && string.Equals(classification.TenantField, source!.TenantField, StringComparison.Ordinal)
            && string.Equals(classification.SiteField, source.SiteField, StringComparison.Ordinal)
            && HasRequiredSourcePredicates(statement, classifier, source);
    }

    private static bool HasRequiredSourcePredicates(
        string statement,
        IContentViewStatementClassifier? classifier,
        ContentViewSourceDefinition source)
        => source.RequiredBooleanPredicates is not { Count: > 0 } predicates
            || classifier is IContentViewRequiredPredicateClassifier predicateClassifier
            && predicates.All(predicate => predicateClassifier.HasBoundBooleanEquality(
                statement,
                predicate.Field,
                predicate.Value));

    /// <summary>
    /// Checks an additional flat WHERE equality after the statement classifier has already proven
    /// that the query is a single un-nested scoped SELECT. This is intentionally narrow.
    /// </summary>
    public static bool HasRequiredBoundEquality(
        string statement,
        string field,
        string parameter,
        IContentViewStatementClassifier? classifier)
        => classifier is IContentViewBoundParameterClassifier structural
            && structural.HasBoundEquality(statement, field, parameter);

    public static bool HasRequiredBoundParameter(
        string statement,
        string parameter,
        IContentViewStatementClassifier? classifier)
        => classifier is IContentViewBoundParameterClassifier structural
            && structural.HasBoundPredicateParameter(statement, parameter);
}

/// <summary>Conservative structural tokenizer. Strings and comments are ignored before statement classification.</summary>
public sealed class SurrealSelectStatementClassifier :
    IContentViewStatementClassifier,
    IContentViewBoundParameterClassifier,
    IContentViewRequiredPredicateClassifier,
    IContentViewRuntimeLimitRewriter,
    IAdminReadOnlyStatementClassifier
{
    private static readonly HashSet<string> Mutations = new(StringComparer.OrdinalIgnoreCase)
    { "CREATE", "UPDATE", "DELETE", "INSERT", "UPSERT", "RELATE", "REMOVE", "DEFINE", "ALTER", "USE", "BEGIN", "COMMIT", "CANCEL", "RETURN", "THROW", "LIVE", "KILL", "OPTION", "INFO" };

    public ContentViewStatementClassification Classify(string statement)
    {
        var tokens = Tokenize(statement, out var semicolons, out var malformed);
        if (tokens.Count == 0) return new(false, false, semicolons > 1, false);
        var hasMutation = tokens.Any(token => token.Kind == TokenKind.Word && Mutations.Contains(token.Value));
        var multiple = semicolons > 1 || (semicolons == 1 && !statement.TrimEnd().EndsWith(';'));
        var hasUnsafeStructure = malformed
            || tokens.Any(token => token.Kind is TokenKind.OpenParenthesis or TokenKind.CloseParenthesis)
            || tokens.Any(token => token.Kind == TokenKind.Word && (token.Value.Contains('.', StringComparison.Ordinal)
                || string.Equals(token.Value, "OR", StringComparison.OrdinalIgnoreCase)));
        var select = tokens.Count > 0 && IsWord(tokens[0], "SELECT");
        var from = FindClause(tokens, "FROM");
        var where = FindClause(tokens, "WHERE");
        var order = FindClause(tokens, "ORDER");
        var limitIndex = FindClause(tokens, "LIMIT");
        var predicateEnd = order >= 0 ? order : limitIndex;
        var hasSingleRootSource = from >= 0 && where == from + 2
            && tokens[from + 1].Kind == TokenKind.Word
            && !tokens[from + 1].Value.Contains('.', StringComparison.Ordinal);
        var singleClauses = hasSingleRootSource && from >= 0 && where > from && limitIndex > where
            && (order < 0 || order > where && order < limitIndex)
            && FindClause(tokens, "SELECT", 1) < 0 && FindClause(tokens, "FROM", from + 1) < 0
            && FindClause(tokens, "WHERE", where + 1) < 0 && FindClause(tokens, "LIMIT", limitIndex + 1) < 0
            && (order < 0 || FindClause(tokens, "ORDER", order + 1) < 0);
        int? limit = limitIndex + 1 < tokens.Count && tokens[limitIndex + 1].Kind == TokenKind.Word
            && int.TryParse(tokens[limitIndex + 1].Value, out var parsed) && limitIndex + 2 == tokens.Count
            ? (int?)parsed : null;
        string? tenantField = null;
        string? siteField = null;
        var hasSafeProjection = HasSafeRootProjection(tokens, from);
        var hasSafeOrder = order < 0 || HasSafeRootOrder(tokens, order, limitIndex);
        var scoped = select && singleClauses && hasSafeProjection && hasSafeOrder && !hasUnsafeStructure
            && HasRequiredTopLevelScopeConjunction(tokens, where + 1, predicateEnd, out tenantField, out siteField);
        return new(select && singleClauses && hasSafeProjection && !hasMutation && !multiple && !hasUnsafeStructure && limit is not null,
            hasMutation, multiple, scoped, limit,
            hasSingleRootSource ? tokens[from + 1].Value : null,
            scoped,
            tenantField,
            siteField);
    }

    /// <summary>
    /// Allows trusted administrators to preview richer read queries without accidentally making
    /// those queries eligible for public execution. The public classifier above remains the only
    /// path to publication. Scope variables are exact/canonical so request data cannot choose a
    /// tenant or site.
    /// </summary>
    public bool IsSingleReadOnlySelect(string statement)
    {
        // A trusted identity does not override tenant/site isolation. Until a host supplies
        // database-enforced row security or a generated relationship plan, preview accepts only
        // the same structurally scoped single-root read form. It may still target an unregistered
        // source, which remains preview-only because public execution additionally requires a
        // registered source and code-owned output mapping.
        var classification = Classify(statement);
        return classification is
        {
            IsSingleReadOnlySelect: true,
            HasMutation: false,
            HasMultipleStatements: false,
            HasRequiredScopePredicates: true,
            Limit: > 0 and <= 10000
        }
            && IsSafeIdentifier(classification.TenantField)
            && IsSafeIdentifier(classification.SiteField);
    }

    public bool HasBoundEquality(string statement, string field, string parameter)
    {
        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(parameter)) return false;
        var tokens = Tokenize(statement, out _, out var malformed);
        if (malformed || !TryGetPredicateRange(tokens, out var start, out var end)) return false;
        for (var index = start; index + 2 < end; index++)
        {
            if (tokens[index].Kind == TokenKind.Word
                && string.Equals(tokens[index].Value, field, StringComparison.Ordinal)
                && tokens[index + 1].Kind == TokenKind.Operator
                && tokens[index + 1].Value is "=" or "=="
                && tokens[index + 2].Kind == TokenKind.Word
                && string.Equals(tokens[index + 2].Value, parameter, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public bool HasBoundPredicateParameter(string statement, string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return false;
        var tokens = Tokenize(statement, out _, out var malformed);
        if (malformed || !TryGetPredicateRange(tokens, out var start, out var end)) return false;
        for (var index = start; index < end; index++)
        {
            if (tokens[index].Kind == TokenKind.Word
                && string.Equals(tokens[index].Value, parameter, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public bool HasBoundBooleanEquality(string statement, string field, bool value)
    {
        if (!IsSafeIdentifier(field)) return false;
        var tokens = Tokenize(statement, out _, out var malformed);
        if (malformed || !TryGetPredicateRange(tokens, out var start, out var end)) return false;
        var expected = value ? "true" : "false";
        for (var index = start; index + 2 < end; index++)
        {
            if (tokens[index].Kind == TokenKind.Word
                && string.Equals(tokens[index].Value, field, StringComparison.Ordinal)
                && tokens[index + 1].Kind == TokenKind.Operator
                && tokens[index + 1].Value is "=" or "=="
                && tokens[index + 2].Kind == TokenKind.Word
                && string.Equals(tokens[index + 2].Value, expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool TryRewriteTerminalLimit(string statement, int requestedTake, out string rewrittenStatement)
    {
        rewrittenStatement = string.Empty;
        if (requestedTake is <= 0 or > 100) return false;
        var classification = Classify(statement);
        var tokens = Tokenize(statement, out _, out var malformed);
        var limit = FindClause(tokens, "LIMIT");
        if (malformed || !classification.IsSingleReadOnlySelect || limit < 0 || limit + 2 != tokens.Count
            || tokens[limit + 1].Kind != TokenKind.Word
            || !int.TryParse(tokens[limit + 1].Value, out _)) return false;
        var numeric = tokens[limit + 1];
        rewrittenStatement = string.Concat(
            statement[..numeric.Start],
            requestedTake.ToString(System.Globalization.CultureInfo.InvariantCulture),
            statement[(numeric.Start + numeric.Length)..]);
        return Classify(rewrittenStatement) is { IsSingleReadOnlySelect: true, Limit: var rewrittenLimit }
            && rewrittenLimit == requestedTake;
    }

    private static bool TryGetPredicateRange(
        IReadOnlyList<Token> tokens,
        out int start,
        out int end)
    {
        start = 0;
        end = 0;
        var where = FindClause(tokens, "WHERE");
        var limit = FindClause(tokens, "LIMIT");
        var order = FindClause(tokens, "ORDER");
        if (where < 0 || limit <= where + 1 || order >= 0 && (order <= where || order >= limit)) return false;
        start = where + 1;
        end = order >= 0 ? order : limit;
        return true;
    }

    private static int FindClause(IReadOnlyList<Token> tokens, string clause, int start = 0)
    {
        for (var i = start; i < tokens.Count; i++)
            if (IsWord(tokens[i], clause)) return i;
        return -1;
    }

    private static bool HasSafeRootProjection(IReadOnlyList<Token> tokens, int from)
    {
        if (from <= 1) return false;
        if (from == 2 && tokens[1].Kind == TokenKind.Star) return true;
        var expectField = true;
        for (var index = 1; index < from; index++)
        {
            if (expectField && tokens[index].Kind == TokenKind.Word && !tokens[index].Value.Contains('.', StringComparison.Ordinal))
            {
                expectField = false;
                continue;
            }
            if (!expectField && tokens[index].Kind == TokenKind.Comma)
            {
                expectField = true;
                continue;
            }
            return false;
        }
        return !expectField;
    }

    private static bool HasSafeRootOrder(IReadOnlyList<Token> tokens, int order, int limit)
    {
        if (order < 0 || limit <= order + 2 || !IsWord(tokens[order + 1], "BY")) return false;
        var expectField = true;
        for (var index = order + 2; index < limit; index++)
        {
            var token = tokens[index];
            if (expectField && token.Kind == TokenKind.Word && IsSafeIdentifier(token.Value))
            {
                expectField = false;
                continue;
            }
            if (!expectField && token.Kind == TokenKind.Word
                && (IsWord(token, "ASC") || IsWord(token, "DESC")))
                continue;
            if (!expectField && token.Kind == TokenKind.Comma)
            {
                expectField = true;
                continue;
            }
            return false;
        }
        return !expectField;
    }

    /// <summary>
    /// The WHERE expression deliberately accepts only a flat AND conjunction of equality terms.
    /// This prevents a projected expression, an OR branch, nested select, or precedence trick from
    /// masquerading as a tenant/site predicate.
    /// </summary>
    private static bool HasRequiredTopLevelScopeConjunction(IReadOnlyList<Token> tokens, int start, int end, out string? tenantField, out string? siteField)
    {
        tenantField = null;
        siteField = null;
        var hasTenant = false;
        var hasSite = false;
        var index = start;
        while (index < end)
        {
            if (index + 2 >= end || tokens[index].Kind != TokenKind.Word
                || tokens[index + 1].Kind is not (TokenKind.Operator or TokenKind.Word)
                || tokens[index + 2].Kind is not (TokenKind.Word or TokenKind.Literal))
                return false;
            var left = tokens[index].Value;
            var right = tokens[index + 2].Kind == TokenKind.Word ? tokens[index + 2].Value : string.Empty;
            var isEquality = tokens[index + 1].Value is "=" or "==";
            if (!isEquality && !IsSafePredicateOperator(tokens[index + 1])) return false;
            if (isEquality && string.Equals(right, ReservedContentViewScopeBinder.TenantParameter, StringComparison.Ordinal)) { hasTenant = true; tenantField = left; }
            else if (isEquality && string.Equals(right, ReservedContentViewScopeBinder.SiteParameter, StringComparison.Ordinal)) { hasSite = true; siteField = left; }
            index += 3;
            if (index == end) break;
            if (!IsWord(tokens[index], "AND")) return false;
            index++;
        }
        return hasTenant && hasSite && tenantField is not null && siteField is not null;
    }

    private static bool IsSafePredicateOperator(Token token)
        => token.Kind == TokenKind.Operator || (token.Kind == TokenKind.Word
            && (string.Equals(token.Value, "CONTAINS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token.Value, "INSIDE", StringComparison.OrdinalIgnoreCase)));

    private static bool IsWord(Token token, string value)
        => token.Kind == TokenKind.Word && string.Equals(token.Value, value, StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && (char.IsLetter(value[0]) || value[0] == '_')
            && value.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static List<Token> Tokenize(string statement, out int semicolons, out bool malformed)
    {
        semicolons = 0; malformed = false; var result = new List<Token>();
        if (string.IsNullOrWhiteSpace(statement)) return result;
        for (var i = 0; i < statement.Length;)
        {
            var c = statement[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '-' && i + 1 < statement.Length && statement[i + 1] == '-') { i += 2; while (i < statement.Length && statement[i] is not '\r' and not '\n') i++; continue; }
            if (c == '/' && i + 1 < statement.Length && statement[i + 1] == '*') { var end = statement.IndexOf("*/", i + 2, StringComparison.Ordinal); if (end < 0) { malformed = true; return result; } i = end + 2; continue; }
            if (c is '\'' or '\"' or '`') { var start = i; var quote = c; var closed = false; i++; while (i < statement.Length) { if (statement[i] == '\\') { i += 2; continue; } if (i < statement.Length && statement[i++] == quote) { closed = true; break; } } if (!closed) malformed = true; else result.Add(new(TokenKind.Literal, string.Empty, start, i - start)); continue; }
            if (c == ';') { semicolons++; i++; continue; }
            if (c == '(') { result.Add(new(TokenKind.OpenParenthesis, "(", i, 1)); i++; continue; }
            if (c == ')') { result.Add(new(TokenKind.CloseParenthesis, ")", i, 1)); i++; continue; }
            if (char.IsLetterOrDigit(c) || c is '_' or '$' or '.') { var start = i++; while (i < statement.Length && (char.IsLetterOrDigit(statement[i]) || statement[i] is '_' or '$' or '.')) i++; result.Add(new(TokenKind.Word, statement[start..i], start, i - start)); continue; }
            if (c is '=' or '!' or '<' or '>' or '~') { var start = i++; if (i < statement.Length && statement[i] == '=') i++; result.Add(new(TokenKind.Operator, statement[start..i], start, i - start)); continue; }
            if (c == ',') { result.Add(new(TokenKind.Comma, ",", i, 1)); i++; continue; }
            if (c == '*') { result.Add(new(TokenKind.Star, "*", i, 1)); i++; continue; }
            malformed = true;
            i++;
        }
        return result;
    }

    private enum TokenKind { Word, Literal, Operator, OpenParenthesis, CloseParenthesis, Comma, Star }
    private sealed record Token(TokenKind Kind, string Value, int Start, int Length);
}

public enum ContentRelationshipOwnershipState
{
    ExternalDiscovered = 0,
    CmsDraft = 1,
    Applied = 2,
    Derived = 3,
    Drifted = 4,
    Adopted = 5
}
public enum ContentRelationshipCardinality { OneToOne, OneToMany, ManyToOne, ManyToMany }
public enum ContentRelationshipKind { FieldJoin, RecordLink, GraphEdge, SelfHierarchy, AssociationRecord }

/// <summary>Metadata for a relationship. Population is intentionally outside DDL lifecycle operations.</summary>
public sealed record ContentRelationshipDefinition(
    long Id,
    ContentViewScope Scope,
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
    string SchemaFingerprint)
{
    public bool IsReadOnly => OwnershipState is ContentRelationshipOwnershipState.ExternalDiscovered
        or ContentRelationshipOwnershipState.Adopted
        or ContentRelationshipOwnershipState.Derived;
    public bool IsMutationBlocked => OwnershipState is ContentRelationshipOwnershipState.Applied
        or ContentRelationshipOwnershipState.Adopted
        or ContentRelationshipOwnershipState.Drifted;
}

public sealed record RelationshipDdlPreview(ContentRelationshipDefinition Relationship, string ProposedSchemaFingerprint, IReadOnlyList<string> Statements);
public sealed record RelationshipDdlApplyJournal(long RelationshipId, ContentViewScope Scope, string AppliedSchemaFingerprint, DateTimeOffset AppliedOn, string? AppliedBy);

/// <summary>
/// A code-owned physical table that CMS relationship metadata is permitted to describe.  A
/// registration is deliberately separate from a site-owned view: database table names are
/// global and must never be nominated by an editor alone.
/// </summary>
public sealed record ContentPhysicalSchemaTarget(
    string ShapeAlias,
    string TableName,
    bool RequiresTenantAndSiteFields = true);

public interface IContentPhysicalSchemaTargetRegistry
{
    IReadOnlyList<ContentPhysicalSchemaTarget> All { get; }
    bool TryGet(string shapeAlias, string tableName, out ContentPhysicalSchemaTarget? target);
    bool TryGetTable(string tableName, out ContentPhysicalSchemaTarget? target);
}

/// <summary>Fail-closed registry used until the consuming host explicitly registers targets.</summary>
public sealed class EmptyContentPhysicalSchemaTargetRegistry : IContentPhysicalSchemaTargetRegistry
{
    public IReadOnlyList<ContentPhysicalSchemaTarget> All => [];
    public bool TryGet(string shapeAlias, string tableName, out ContentPhysicalSchemaTarget? target)
    {
        target = null;
        return false;
    }
    public bool TryGetTable(string tableName, out ContentPhysicalSchemaTarget? target)
    {
        target = null;
        return false;
    }
}

/// <summary>Host registrations for the finite set of physical tables CMS may administer.</summary>
public sealed class ContentPhysicalSchemaTargetRegistry(IEnumerable<ContentPhysicalSchemaTarget> targets) : IContentPhysicalSchemaTargetRegistry
{
    private readonly IReadOnlyList<ContentPhysicalSchemaTarget> all = targets
        .DistinctBy(target => (target.ShapeAlias, target.TableName))
        .OrderBy(target => target.ShapeAlias, StringComparer.Ordinal)
        .ThenBy(target => target.TableName, StringComparer.Ordinal)
        .ToArray();

    private readonly IReadOnlyDictionary<string, IReadOnlyList<ContentPhysicalSchemaTarget>> byTable = targets
        .DistinctBy(target => (target.ShapeAlias, target.TableName))
        .GroupBy(target => target.TableName, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<ContentPhysicalSchemaTarget>)group.OrderBy(target => target.ShapeAlias, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);

    public IReadOnlyList<ContentPhysicalSchemaTarget> All => all;

    public bool TryGet(string shapeAlias, string tableName, out ContentPhysicalSchemaTarget? target)
    {
        target = null;
        if (!byTable.TryGetValue(tableName, out var candidates)) return false;
        target = candidates.SingleOrDefault(candidate =>
            string.Equals(candidate.ShapeAlias, shapeAlias, StringComparison.Ordinal));
        return target is not null;
    }

    public bool TryGetTable(string tableName, out ContentPhysicalSchemaTarget? target)
    {
        target = null;
        if (!byTable.TryGetValue(tableName, out var candidates) || candidates.Count != 1) return false;
        target = candidates[0];
        return true;
    }
}

/// <summary>Authenticated platform actor allowed to apply global schema changes.</summary>
public sealed record ContentSchemaActor(string Subject)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Subject);
}

/// <summary>
/// Reports whether this AeroCMS build can safely administer a database-global relationship
/// schema.  This is deliberately a capability, rather than a configuration switch: an
/// executor credential alone cannot prove atomic CREATE ONLY claim semantics on the active
/// SurrealDB/Sable combination.
/// </summary>
public sealed record ContentRelationshipSchemaCapability(bool IsVerified, string Reason)
{
    public static ContentRelationshipSchemaCapability Unavailable(string reason)
        => new(false, reason);
}

public interface IContentRelationshipSchemaCapabilityProvider
{
    ContentRelationshipSchemaCapability Current { get; }
}

/// <summary>
/// Raw-client transactions have narrower semantics than the CMS protocol. Keep physical DDL
/// disabled rather than allowing a host setting to bypass the unverified global-claim,
/// same-scope assertion, verification, and two-site convergence invariants.
/// </summary>
public sealed class DisabledContentRelationshipSchemaCapabilityProvider : IContentRelationshipSchemaCapabilityProvider
{
    public static readonly ContentRelationshipSchemaCapability Capability = ContentRelationshipSchemaCapability.Unavailable(
        "CMS-managed physical relationship DDL requires a verified atomic CREATE ONLY global schema claim, same-scope assertion, post-DDL verification, and two-site convergence protocol.");

    public ContentRelationshipSchemaCapability Current => Capability;
}

/// <summary>
/// Host-owned atomic boundary for schema DDL and the durable relationship lock/journal.  It must
/// use one database session and explicit transaction; the CMS intentionally has no fallback
/// that can execute DDL and journal it in separate commits.
/// </summary>
public interface IContentRelationshipSchemaApplyCoordinator
{
    bool IsEnabled { get; }
    Task<RelationshipDdlApplyJournal> ApplyAtomicallyAsync(RelationshipDdlPreview preview, ContentSchemaActor actor, CancellationToken ct = default);
}

public sealed class DisabledContentRelationshipSchemaApplyCoordinator : IContentRelationshipSchemaApplyCoordinator
{
    public bool IsEnabled => false;
    public Task<RelationshipDdlApplyJournal> ApplyAtomicallyAsync(RelationshipDdlPreview preview, ContentSchemaActor actor, CancellationToken ct = default)
        => Task.FromException<RelationshipDdlApplyJournal>(new InvalidOperationException("A host-owned atomic schema apply coordinator is required."));
}

public interface IContentRelationshipStore
{
    Task<ContentRelationshipDefinition?> LoadAsync(ContentViewScope scope, string alias, CancellationToken ct = default);
    Task<IReadOnlyList<ContentRelationshipDefinition>> ListAsync(ContentViewScope scope, CancellationToken ct = default);
    Task<ContentRelationshipDefinition> SaveDraftAsync(ContentRelationshipDefinition relationship, CancellationToken ct = default);
    Task<ContentRelationshipDefinition> AdoptAsync(ContentRelationshipDefinition relationship, CancellationToken ct = default);
    Task<RelationshipDdlApplyJournal> SaveAppliedAsync(RelationshipDdlApplyJournal journal, CancellationToken ct = default);
    Task MarkDriftedAsync(ContentViewScope scope, long relationshipId, string observedSchemaFingerprint, CancellationToken ct = default);
}

/// <summary>Lists and resolves site-owned virtual entry providers without exposing persistence details.</summary>
public interface IContentEntrySourceProviderCatalog
{
    Task<IReadOnlyList<string>> ListProviderKeysAsync(ContentViewScope scope, CancellationToken ct = default);
    Task<IContentEntrySourceProvider?> ResolveAsync(ContentViewScope scope, string provider, CancellationToken ct = default);
}

/// <summary>Executes schema commands through an explicitly configured privileged identity.</summary>
public interface IPrivilegedContentSchemaCommandExecutor
{
    bool IsEnabled { get; }
    Task ExecuteAsync(IReadOnlyList<string> statements, CancellationToken ct = default);
}

/// <summary>Default deny-all executor; hosts must opt in with a dedicated schema identity.</summary>
public sealed class DisabledContentSchemaCommandExecutor : IPrivilegedContentSchemaCommandExecutor
{
    public bool IsEnabled => false;
    public Task ExecuteAsync(IReadOnlyList<string> statements, CancellationToken ct = default)
        => Task.FromException(new InvalidOperationException("A separately configured privileged schema command executor is required."));
}

/// <summary>Discovers schema-owned links. Discovered relationships are external and cannot be edited by CMS.</summary>
public interface IContentRelationshipSchemaDiscovery
{
    Task<IReadOnlyList<ContentRelationshipDefinition>> DiscoverAsync(ContentViewScope scope, CancellationToken ct = default);
}

/// <summary>Read-only schema metadata boundary. Implementations must issue only INFO statements.</summary>
public interface IContentSchemaMetadataReader
{
    Task<IReadOnlyDictionary<string, string>> ReadTableDefinitionsAsync(CancellationToken ct = default);
}

/// <summary>DDL lifecycle is schema-only; implementations must never populate or RELATE records through this contract.</summary>
public interface IRelationshipDdlLifecycle
{
    Task<RelationshipDdlPreview> PreviewAsync(ContentRelationshipDefinition relationship, CancellationToken ct = default);
    /// <summary>Applies only after the endpoint independently verifies the platform schema claim.</summary>
    Task<RelationshipDdlApplyJournal> ApplyAsync(RelationshipDdlPreview preview, ContentSchemaActor actor, CancellationToken ct = default);
    [Obsolete("Schema application requires an authenticated ContentSchemaActor.")]
    Task<RelationshipDdlApplyJournal> ApplyAsync(RelationshipDdlPreview preview, CancellationToken ct = default);
}
