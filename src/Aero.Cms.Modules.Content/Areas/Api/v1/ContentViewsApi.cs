using System.Security.Claims;
using System.Text.Json;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Infrastructure;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Content.Areas.Api.v1;

/// <summary>Authenticated, site-scoped manager endpoints for query-backed content views.</summary>
public static class ContentViewsApi
{
    private const int MaximumPreviewTake = 50;
    private const int MaximumCacheDurationSeconds = 86_400;

    public static void MapContentViewsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/content-views")
            .WithTags("Admin - Content Views")
            .RequireAuthorization()
            .AddEndpointFilter<SelectedSiteScopeEndpointFilter>();

        group.MapGet("/shapes", ListShapes).RequireAuthorization("site:read").RequireAuthorization("AeroAdmin");
        group.MapGet("/{alias}", GetDraft).RequireAuthorization("site:read").RequireAuthorization("AeroAdmin");
        group.MapPut("/{alias}/draft", SaveDraft).RequireAuthorization("site:update").RequireAuthorization("AeroAdmin");
        group.MapPost("/{alias}/preview", Preview).RequireAuthorization("site:read").RequireAuthorization("AeroAdmin");
        group.MapPost("/{alias}/publish", Publish).RequireAuthorization("site:update").RequireAuthorization("AeroAdmin");
        group.MapPost("/{alias}/cache/invalidate", InvalidateCache).RequireAuthorization("site:update").RequireAuthorization("AeroAdmin");
        group.MapGet("/{alias}/relationships", ListRelationships).RequireAuthorization("site:read").RequireAuthorization("AeroAdmin");
        group.MapPut("/{alias}/relationships/{relationshipAlias}/draft", SaveRelationshipDraft)
            .RequireAuthorization("site:update")
            .RequireAuthorization("AeroAdmin");
        group.MapPost("/{alias}/relationships/{relationshipId:long}/ddl/preview", PreviewRelationshipDdl)
            .RequireAuthorization("site:update")
            .RequireAuthorization("AeroAdmin");
        group.MapPost("/{alias}/relationships/{relationshipId:long}/ddl/apply", ApplyRelationshipDdl)
            .RequireAuthorization("site:update")
            .RequireAuthorization("AeroAdmin")
            // DDL is database-global.  Site ownership is insufficient authority.
            .RequireAuthorization(policy => policy.RequireClaim("aero:schema:ddl", "true"));
        group.MapGet("/entries/{provider}", SearchVirtualEntries).RequireAuthorization("site:read");
        group.MapGet("/entries/{provider}/{stableId}", GetVirtualEntry).RequireAuthorization("site:read");
        group.MapGet("/entries", ListVirtualEntryProviders).RequireAuthorization("site:read");
    }


    private static IResult ListShapes(
        [FromServices] IContentShapeRegistry registry,
        [FromServices] ISiteContext siteContext)
    {
        if (!TryCreateScope(siteContext, out _, out var failure)) return failure!;
        if (!registry.IsValid)
        {
            return TypedResults.Problem(
                title: "Content shape registry is invalid",
                detail: string.Join("; ", registry.Errors),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return TypedResults.Ok<IReadOnlyList<ContentViewShapeOption>>(
            registry.Definitions
                .Select(shape => new ContentViewShapeOption(shape.Alias, shape.SchemaFingerprint, shape.Fields))
                .ToArray());
    }

    private static async Task<IResult> GetDraft(
        string alias,
        [FromServices] IContentSurrealViewService service,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var draft = await service.LoadDraftAsync(scope, alias, ct);
        return draft is null ? TypedResults.NotFound() : TypedResults.Ok(Map(draft));
    }

    private static async Task<IResult> SaveDraft(
        string alias,
        [FromBody] SaveContentViewDraftRequest request,
        [FromServices] IContentSurrealViewService service,
        [FromServices] IContentShapeRegistry registry,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        if (!TryValidateDraftRequest(
                alias,
                request.ShapeAlias,
                request.SelectStatement,
                request.IdentityField,
                request.TitleField,
                request.EntrySelectStatement,
                request.SearchSelectStatement,
                request.CacheDurationSeconds,
                registry,
                out var shape,
                out failure))
            return failure!;

        var existing = await service.LoadDraftAsync(scope, alias, ct);
        var draft = new ContentSurrealViewRevision(
            existing?.Id ?? 0,
            scope,
            alias.Trim(),
            shape!.Alias,
            shape.SchemaFingerprint,
            request.SelectStatement.Trim(),
            request.IdentityField.Trim(),
            string.IsNullOrWhiteSpace(request.TitleField) ? null : request.TitleField.Trim(),
            existing?.Version ?? 0,
            ContentViewPublicationState.Draft,
            existing?.CreatedOn ?? DateTimeOffset.UtcNow,
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.User.Identity?.Name,
            request.CacheEnabled,
            TimeSpan.FromSeconds(request.CacheDurationSeconds),
            existing?.CacheGeneration ?? 0,
            request.EntrySelectStatement.Trim(),
            request.SearchSelectStatement.Trim());
        var saved = await service.SaveDraftAsync(draft, MaximumPreviewTake, ct);
        return saved is null
            ? TypedResults.BadRequest(new ProblemDetails
            {
                Title = "The view draft is not executable",
                Detail = "Use one scoped SELECT statement with an explicit LIMIT no greater than 50.",
                Status = StatusCodes.Status400BadRequest
            })
            : TypedResults.Ok(Map(saved));
    }

    private static async Task<IResult> Preview(
        string alias,
        [FromBody] PreviewContentViewRequest request,
        [FromServices] IContentSurrealViewService service,
        [FromServices] IContentShapeRegistry registry,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        if (!TryValidatePreviewRequest(alias, request.ShapeAlias, request.SelectStatement, registry, out var shape, out failure))
            return failure!;

        var take = Math.Clamp(request.Take, 1, MaximumPreviewTake);
        var previewRevision = new ContentSurrealViewRevision(
            0,
            scope,
            alias.Trim(),
            shape!.Alias,
            shape.SchemaFingerprint,
            request.SelectStatement.Trim(),
            string.Empty,
            null,
            0,
            ContentViewPublicationState.Draft,
            DateTimeOffset.UtcNow);
        try
        {
            var result = await service.PreviewAsync(
                previewRevision,
                scope,
                new Dictionary<string, object?>(StringComparer.Ordinal),
                take,
                MaximumPreviewTake,
                ct);
            if (result is null)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Preview rejected",
                    Detail = "The query must be a single tenant- and site-scoped SELECT with a bounded LIMIT.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var rows = result.Rows
                .Select(row => (IReadOnlyDictionary<string, JsonElement>)row.ToDictionary(
                    pair => pair.Key,
                    pair => JsonSerializer.SerializeToElement(pair.Value),
                    StringComparer.Ordinal))
                .ToArray();
            var outputFields = rows.SelectMany(row => row.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(field => field, StringComparer.Ordinal)
                .ToArray();
            return TypedResults.Ok(new ContentViewPreviewResponse(rows, outputFields, result.IsTruncated));
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.Problem(
                title: "Read-only view execution is unavailable",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> Publish(
        string alias,
        [FromBody] PublishContentViewRequest request,
        [FromServices] IContentSurrealViewService service,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var published = await service.PublishAsync(scope, alias, request.DraftVersion, ct);
        return published is null
            ? TypedResults.Conflict(new ProblemDetails
            {
                Title = "The view draft changed",
                Detail = "Reload the latest draft before publishing.",
                Status = StatusCodes.Status409Conflict
            })
            : TypedResults.Ok(Map(published));
    }

    private static async Task<IResult> InvalidateCache(
        string alias,
        [FromServices] IContentSurrealViewService service,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var draft = await service.LoadDraftAsync(scope, alias, ct);
        if (draft is null) return TypedResults.NotFound();
        await service.InvalidateAsync(scope, ct);
        return TypedResults.Ok(new ContentViewCacheInvalidationResponse(true, draft.CacheGeneration));
    }

    private static async Task<IResult> ListRelationships(
        string alias,
        [FromServices] IContentSurrealViewService viewService,
        [FromServices] ISiteContext siteContext,
        IServiceProvider services,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var draft = await viewService.LoadDraftAsync(scope, alias, ct);
        if (draft is null) return TypedResults.NotFound();

        var store = services.GetService<IContentRelationshipStore>();
        var discovery = services.GetService<IContentRelationshipSchemaDiscovery>();
        var managed = store is null ? [] : await store.ListAsync(scope, ct);
        var discovered = discovery is null ? [] : await discovery.DiscoverAsync(scope, ct);
        if (store is not null)
        {
            foreach (var applied in managed.Where(item => item.OwnershipState == ContentRelationshipOwnershipState.Applied))
            {
                var observed = discovered.FirstOrDefault(item => SamePhysicalRelationship(item, applied));
                await store.MarkDriftedAsync(scope, applied.Id, observed?.SchemaFingerprint ?? "MISSING", ct);
            }
            managed = await store.ListAsync(scope, ct);
        }
        var relationships = managed.Concat(discovered.Where(discoveredRelationship => !managed.Any(managedRelationship => SamePhysicalRelationship(managedRelationship, discoveredRelationship))))
            .Where(relationship =>
                string.Equals(relationship.SourceShapeAlias, draft.ShapeAlias, StringComparison.Ordinal)
                || string.Equals(relationship.TargetShapeAlias, draft.ShapeAlias, StringComparison.Ordinal))
            .DistinctBy(relationship => relationship.Id)
            .OrderBy(relationship => relationship.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(Map)
            .ToArray();
        return TypedResults.Ok<IReadOnlyList<ContentRelationshipSummary>>(relationships);
    }

    private static async Task<IResult> SaveRelationshipDraft(
        string alias,
        string relationshipAlias,
        [FromBody] SaveContentRelationshipDraftRequest request,
        [FromServices] IContentSurrealViewService viewService,
        [FromServices] IContentShapeRegistry shapeRegistry,
        [FromServices] ISiteContext siteContext,
        IServiceProvider services,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var view = await viewService.LoadDraftAsync(scope, alias, ct);
        if (view is null) return TypedResults.NotFound();
        var store = services.GetService<IContentRelationshipStore>();
        var lifecycle = services.GetService<IRelationshipDdlLifecycle>();
        if (store is null || lifecycle is null)
        {
            return TypedResults.Problem(
                title: "Relationship editing is unavailable",
                detail: "This host has not configured the relationship draft lifecycle.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var normalizedAlias = relationshipAlias.Trim();
        if (!IsSimpleIdentifier(normalizedAlias)
            || !IsSimpleIdentifier(request.SourceTable)
            || !IsSimpleIdentifier(request.TargetTable)
            || !IsOptionalSimpleIdentifier(request.SourceField)
            || !IsOptionalSimpleIdentifier(request.TargetField)
            || !IsOptionalSimpleIdentifier(request.EdgeTable)
            || !IsOptionalRegisteredShape(request.SourceShapeAlias, shapeRegistry)
            || !IsOptionalRegisteredShape(request.TargetShapeAlias, shapeRegistry)
            || string.IsNullOrWhiteSpace(request.SourceShapeAlias)
                && string.IsNullOrWhiteSpace(request.TargetShapeAlias)
            || !string.Equals(request.SourceShapeAlias, view.ShapeAlias, StringComparison.Ordinal)
                && !string.Equals(request.TargetShapeAlias, view.ShapeAlias, StringComparison.Ordinal))
        {
            return InvalidRelationshipDraft();
        }

        var existing = await store.LoadAsync(scope, normalizedAlias, ct);
        if (existing is not null && existing.OwnershipState != ContentRelationshipOwnershipState.CmsDraft)
            return RelationshipMutationBlocked();

        var candidate = new ContentRelationshipDefinition(
            existing?.Id ?? 0,
            scope,
            normalizedAlias,
            NormalizeOptional(request.SourceShapeAlias),
            NormalizeOptional(request.TargetShapeAlias),
            request.SourceTable.Trim(),
            request.TargetTable.Trim(),
            NormalizeOptional(request.SourceField),
            NormalizeOptional(request.TargetField),
            NormalizeOptional(request.EdgeTable),
            request.Kind,
            request.Cardinality,
            ContentRelationshipOwnershipState.CmsDraft,
            string.Empty);
        // Existing database schema is immutable from the CMS even when an editor chooses a new
        // alias. The physical identity is what matters, not the site-owned display alias.
        var discovery = services.GetService<IContentRelationshipSchemaDiscovery>();
        if (discovery is not null
            && (await discovery.DiscoverAsync(scope, ct)).Any(discovered => SamePhysicalRelationship(discovered, candidate)))
            return RelationshipMutationBlocked();
        try
        {
            var preview = await lifecycle.PreviewAsync(candidate, ct);
            if (preview.Statements.Count == 0 && candidate.Kind != ContentRelationshipKind.FieldJoin) return InvalidRelationshipDraft();
            var saved = await store.SaveDraftAsync(candidate with { SchemaFingerprint = preview.ProposedSchemaFingerprint }, ct);
            return TypedResults.Ok(Map(saved));
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid relationship draft",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    private static async Task<IResult> PreviewRelationshipDdl(
        string alias,
        long relationshipId,
        [FromServices] IContentSurrealViewService viewService,
        [FromServices] ISiteContext siteContext,
        IServiceProvider services,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var view = await viewService.LoadDraftAsync(scope, alias, ct);
        if (view is null) return TypedResults.NotFound();
        var relationship = await FindManagedRelationshipAsync(scope, relationshipId, services, ct);
        if (relationship is null) return TypedResults.NotFound();
        if (!BelongsToViewShape(relationship, view.ShapeAlias)) return TypedResults.NotFound();
        if (relationship.OwnershipState != ContentRelationshipOwnershipState.CmsDraft)
            return RelationshipMutationBlocked();
        if (relationship.Kind == ContentRelationshipKind.FieldJoin)
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Field joins are metadata-only",
                Detail = "This relationship is saved as CMS metadata and has no physical SurrealDB DDL to preview.",
                Status = StatusCodes.Status409Conflict
            });

        return PhysicalRelationshipSchemaUnavailable();
    }

    private static async Task<IResult> ApplyRelationshipDdl(
        string alias,
        long relationshipId,
        [FromBody] ApplyRelationshipDdlRequest request,
        [FromServices] IContentSurrealViewService viewService,
        [FromServices] ISiteContext siteContext,
        HttpContext httpContext,
        IServiceProvider services,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var view = await viewService.LoadDraftAsync(scope, alias, ct);
        if (view is null) return TypedResults.NotFound();
        var relationship = await FindManagedRelationshipAsync(scope, relationshipId, services, ct);
        var lifecycle = services.GetService<IRelationshipDdlLifecycle>();
        if (relationship is null || lifecycle is null) return TypedResults.NotFound();
        if (!BelongsToViewShape(relationship, view.ShapeAlias)) return TypedResults.NotFound();
        if (relationship.OwnershipState != ContentRelationshipOwnershipState.CmsDraft)
            return RelationshipMutationBlocked();
        if (relationship.Kind == ContentRelationshipKind.FieldJoin)
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Field joins are metadata-only",
                Detail = "This relationship is saved as CMS metadata and cannot apply SurrealDB DDL.",
                Status = StatusCodes.Status409Conflict
            });

        // Physical SurrealDB schema is database-global while relationship drafts are site-owned.
        // Until the exact supported SurrealDB runtime proves endpoint tenant/site equality and a
        // database-global ownership claim can be applied atomically, both preview and apply remain
        // unavailable.  Saving relationship metadata and discovering existing schema are safe.
        _ = request;
        _ = httpContext;
        _ = lifecycle;
        return PhysicalRelationshipSchemaUnavailable();
    }

    private static async Task<IResult> SearchVirtualEntries(
        string provider,
        [FromServices] IEnumerable<IContentEntrySourceProvider> providers,
        [FromServices] IContentEntrySourceProviderCatalog catalog,
        [FromServices] ISiteContext siteContext,
        [FromQuery] string? culture = null,
        [FromQuery] string? query = null,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var source = await ResolveProviderAsync(providers, catalog, scope, provider, ct);
        if (source is null) return TypedResults.NotFound();
        var entries = await source.SearchAsync(scope, culture, query, Math.Clamp(take, 1, 100), ct);
        var options = entries
            .Where(entry => entry.Scope == scope && entry.Key.IsValid && string.Equals(entry.Key.Provider, source.Provider, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new VirtualContentEntryOption(
                entry.Key.Provider,
                entry.Key.StableId,
                FindDisplayValue(entry.Values, "title", "name", "scientificName", "label") ?? entry.Key.StableId,
                FindDisplayValue(entry.Values, "subtitle", "description", "slug")))
            .ToArray();
        return TypedResults.Ok<IReadOnlyList<VirtualContentEntryOption>>(options);
    }

    private static async Task<IResult> ListVirtualEntryProviders(
        [FromServices] IEnumerable<IContentEntrySourceProvider> providers,
        [FromServices] IContentEntrySourceProviderCatalog catalog,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var dynamicProviders = await catalog.ListProviderKeysAsync(scope, ct);
        var options = providers
            .Select(provider => provider.Provider)
            .Concat(dynamicProviders)
            .Where(provider => !string.IsNullOrWhiteSpace(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(provider => provider, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new ContentEntryProviderOption(provider, provider))
            .ToArray();
        return TypedResults.Ok<IReadOnlyList<ContentEntryProviderOption>>(options);
    }

    private static async Task<IResult> GetVirtualEntry(
        string provider,
        string stableId,
        [FromServices] IEnumerable<IContentEntrySourceProvider> providers,
        [FromServices] IContentEntrySourceProviderCatalog catalog,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!TryCreateScope(siteContext, out var scope, out var failure)) return failure!;
        var source = await ResolveProviderAsync(providers, catalog, scope, provider, ct);
        if (source is null) return TypedResults.NotFound();
        var entry = await source.FindAsync(scope, stableId, ct);
        if (entry is null
            || entry.Scope != scope
            || !entry.Key.IsValid
            || !string.Equals(entry.Key.Provider, source.Provider, StringComparison.OrdinalIgnoreCase))
            return TypedResults.NotFound();

        return TypedResults.Ok(new VirtualContentEntryDetail(
            entry.Key.Provider,
            entry.Key.StableId,
            entry.Values.ToDictionary(
                pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value),
                StringComparer.Ordinal)));
    }

    private static IContentEntrySourceProvider? FindProvider(
        IEnumerable<IContentEntrySourceProvider> providers,
        string provider)
        => providers.FirstOrDefault(candidate => string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase));

    private static async Task<IContentEntrySourceProvider?> ResolveProviderAsync(
        IEnumerable<IContentEntrySourceProvider> providers,
        IContentEntrySourceProviderCatalog catalog,
        ContentViewScope scope,
        string provider,
        CancellationToken ct)
        => FindProvider(providers, provider) ?? await catalog.ResolveAsync(scope, provider, ct);

    private static string? FindDisplayValue(
        IReadOnlyDictionary<string, object?> values,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            var pair = values.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value is { } value)
            {
                var text = value is JsonElement { ValueKind: JsonValueKind.String } json
                    ? json.GetString()
                    : value.ToString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }

        return null;
    }

    private static async Task<ContentRelationshipDefinition?> FindManagedRelationshipAsync(
        ContentViewScope scope,
        long relationshipId,
        IServiceProvider services,
        CancellationToken ct)
    {
        var store = services.GetService<IContentRelationshipStore>();
        if (store is null) return null;
        return (await store.ListAsync(scope, ct)).FirstOrDefault(relationship => relationship.Id == relationshipId);
    }

    private static bool BelongsToViewShape(ContentRelationshipDefinition relationship, string shapeAlias)
        => string.Equals(relationship.SourceShapeAlias, shapeAlias, StringComparison.Ordinal)
            || string.Equals(relationship.TargetShapeAlias, shapeAlias, StringComparison.Ordinal);

    private static bool SamePhysicalRelationship(ContentRelationshipDefinition left, ContentRelationshipDefinition right)
        => left.Kind == right.Kind
            && string.Equals(left.SourceTable, right.SourceTable, StringComparison.Ordinal)
            && string.Equals(left.TargetTable, right.TargetTable, StringComparison.Ordinal)
            && string.Equals(left.SourceField, right.SourceField, StringComparison.Ordinal)
            && string.Equals(left.TargetField, right.TargetField, StringComparison.Ordinal)
            && string.Equals(left.EdgeTable, right.EdgeTable, StringComparison.Ordinal)
            && left.Cardinality == right.Cardinality;

    private static bool TryValidateDraftRequest(
        string alias,
        string shapeAlias,
        string selectStatement,
        string identityField,
        string? titleField,
        string entrySelectStatement,
        string searchSelectStatement,
        int cacheDurationSeconds,
        IContentShapeRegistry registry,
        out ContentShapeDefinition? shape,
        out IResult? failure)
    {
        shape = null;
        failure = null;
        if (string.IsNullOrWhiteSpace(alias)
            || string.IsNullOrWhiteSpace(shapeAlias)
            || string.IsNullOrWhiteSpace(selectStatement)
            || string.IsNullOrWhiteSpace(identityField)
            || string.IsNullOrWhiteSpace(entrySelectStatement)
            || string.IsNullOrWhiteSpace(searchSelectStatement)
            || cacheDurationSeconds is < 1 or > MaximumCacheDurationSeconds
            || !registry.TryGet(shapeAlias, out shape)
            || !shape!.Fields.Any(field => string.Equals(field.Name, identityField, StringComparison.Ordinal))
            || !string.IsNullOrWhiteSpace(titleField)
                && !shape.Fields.Any(field => string.Equals(field.Name, titleField, StringComparison.Ordinal)))
        {
            failure = TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid content view draft",
                Detail = "Choose a registered shape, map an identity field, enter bounded SELECT queries for listing, exact entry, and search, and use a cache duration between 1 second and 24 hours.",
                Status = StatusCodes.Status400BadRequest
            });
            return false;
        }

        return true;
    }

    private static bool TryValidatePreviewRequest(
        string alias,
        string shapeAlias,
        string selectStatement,
        IContentShapeRegistry registry,
        out ContentShapeDefinition? shape,
        out IResult? failure)
    {
        shape = null;
        failure = null;
        if (!string.IsNullOrWhiteSpace(alias)
            && !string.IsNullOrWhiteSpace(shapeAlias)
            && !string.IsNullOrWhiteSpace(selectStatement)
            && registry.TryGet(shapeAlias, out shape)) return true;

        failure = TypedResults.BadRequest(new ProblemDetails
        {
            Title = "Invalid content view preview",
            Detail = "Choose a registered shape and enter a bounded SELECT query.",
            Status = StatusCodes.Status400BadRequest
        });
        return false;
    }

    private static bool TryCreateScope(ISiteContext siteContext, out ContentViewScope scope, out IResult? failure)
    {
        scope = new ContentViewScope(siteContext.TenantId, siteContext.SiteId);
        failure = scope.IsValid
            ? null
            : TypedResults.BadRequest(new ProblemDetails
            {
                Title = "No current site selected",
                Detail = "Select an authorized site before managing content views.",
                Status = StatusCodes.Status400BadRequest
            });
        return scope.IsValid;
    }

    private static ContentViewEditorSnapshot Map(ContentSurrealViewRevision view) => new(
        view.Alias,
        view.ShapeAlias,
        view.ShapeFingerprint,
        view.SelectStatement,
        view.IdentityField,
        view.TitleField,
        view.EntrySelectStatement ?? string.Empty,
        view.SearchSelectStatement ?? string.Empty,
        view.Version,
        view.PublicationState,
        view.CacheEnabled,
        (int)Math.Clamp((view.CacheDuration ?? TimeSpan.FromMinutes(5)).TotalSeconds, 1, MaximumCacheDurationSeconds),
        view.CacheGeneration,
        view.PublicExecutionEligible,
        view.PublicExecutionIneligibilityReason);

    private static ContentRelationshipSummary Map(ContentRelationshipDefinition relationship) => new(
        relationship.Id,
        relationship.Alias,
        relationship.SourceShapeAlias,
        relationship.TargetShapeAlias,
        relationship.SourceTable,
        relationship.TargetTable,
        relationship.SourceField,
        relationship.TargetField,
        relationship.EdgeTable,
        relationship.Kind,
        relationship.Cardinality,
        relationship.OwnershipState,
        relationship.SchemaFingerprint,
        false,
        false);

    private static bool IsSimpleIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && char.IsLetter(value[0])
            && value.Length <= 63
            && value.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static bool IsOptionalSimpleIdentifier(string? value)
        => string.IsNullOrWhiteSpace(value) || IsSimpleIdentifier(value);

    private static bool IsOptionalRegisteredShape(string? alias, IContentShapeRegistry registry)
        => string.IsNullOrWhiteSpace(alias) || registry.TryGet(alias, out _);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult InvalidRelationshipDraft() => TypedResults.BadRequest(new ProblemDetails
    {
        Title = "Invalid relationship draft",
        Detail = "Use simple SurrealDB identifiers, associate the relationship with this view's shape, and supply the fields required by the selected relationship kind.",
        Status = StatusCodes.Status400BadRequest
    });

    private static IResult RelationshipMutationBlocked() => TypedResults.Conflict(new ProblemDetails
    {
        Title = "Relationship is read-only or locked",
        Detail = "Only an unapplied CMS draft relationship can generate or apply DDL.",
        Status = StatusCodes.Status409Conflict
    });

    private static IResult PhysicalRelationshipSchemaUnavailable() => TypedResults.Conflict(new ProblemDetails
    {
        Title = "Physical relationship schema is unavailable",
        Detail = "AeroCMS can save relationship metadata and discover existing database relationships, but it will not preview or apply database-global relationship DDL until tenant/site endpoint equality and global schema ownership are verified on the configured SurrealDB runtime.",
        Status = StatusCodes.Status409Conflict
    });
}
