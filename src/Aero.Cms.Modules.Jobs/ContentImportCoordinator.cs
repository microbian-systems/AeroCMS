using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Importing;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Infrastructure;
using System.Text.Json;

namespace Aero.Cms.Modules.Jobs;

/// <summary>Coordinates one leased import without exposing HTTP or persistence implementation details to providers.</summary>
internal sealed class ContentImportCoordinator(
    IEnumerable<IContentTypeImporter> importers,
    IContentImportJobStore jobs,
    ISelectedSiteScopeResolver sites,
    IContentTypeService contentTypes,
    IContentSurrealViewService views) : IContentImportCoordinator
{
    public async Task<ContentImportProviderResult> ExecuteAsync(ContentImportLease lease, CancellationToken ct = default)
    {
        var job = await LoadCurrentAsync(lease, ct);
        if (job is null)
            return ContentImportProviderResult.Failure("The import lease is no longer current.");
        var provider = importers.Where(x => string.Equals(x.Descriptor.Key, job.Request.ImporterKey, StringComparison.Ordinal)
            && string.Equals(x.Descriptor.Version, job.Request.ImporterVersion, StringComparison.Ordinal)).ToArray();
        if (provider.Length != 1) return ContentImportProviderResult.Failure(provider.Length == 0 ? "The pinned importer is not registered." : "Duplicate importers are registered for the pinned key and version.", disposition: ContentImportFailureDisposition.Terminal);
        var selected = await sites.ResolveAsync(job.Request.SiteId, ct);
        if (selected is not { IsValid: true } scope || scope.TenantId != job.TenantId)
            return ContentImportProviderResult.Failure("The requested site no longer resolves to its authoritative tenant scope.", disposition: ContentImportFailureDisposition.Terminal);
        var context = new ContentImportContext(job, new ContentViewScope(scope.TenantId, scope.SiteId));
        var plan = await provider[0].PlanAsync(context, ct);
        if (await LoadCurrentAsync(lease, ct) is null)
            return ContentImportProviderResult.Failure("The import lease was lost before provisioning could begin.");
        var provisioned = await ProvisionAsync(plan, context.Scope, ct);
        if (!provisioned.Succeeded) return provisioned;
        if (await LoadCurrentAsync(lease, ct) is null)
            return ContentImportProviderResult.Failure("The import lease was lost before source data could be imported.");
        var execution = new ContentImportExecutionContext(context, new LeaseProgressSink(jobs, lease));
        var imported = await provider[0].ImportAsync(execution, ct);
        if (!imported.Succeeded) return imported;
        var importReportedProgress = imported.ProgressCurrent != 0 || imported.ProgressTotal.HasValue;
        var completed = !importReportedProgress && (job.ProgressCurrent != 0 || job.ProgressTotal.HasValue)
            ? ContentImportProviderResult.Success(imported.Checkpoint, job.ProgressCurrent, job.ProgressTotal)
            : imported;
        if (!await execution.Progress.ReportAsync(completed.Checkpoint, completed.ProgressCurrent, completed.ProgressTotal, ct))
            return ContentImportProviderResult.Failure("The import lease was lost while recording progress.", completed.Checkpoint);
        if (job.Request.Activate)
        {
            var activated = await provider[0].ActivateAsync(execution, ct);
            if (!activated.Succeeded) return activated;
            var activationReportedProgress = activated.ProgressCurrent != 0 || activated.ProgressTotal.HasValue;
            completed = ContentImportProviderResult.Success(
                activated.Checkpoint ?? completed.Checkpoint,
                activationReportedProgress ? activated.ProgressCurrent : completed.ProgressCurrent,
                activationReportedProgress ? activated.ProgressTotal : completed.ProgressTotal);
            if (!await execution.Progress.ReportAsync(completed.Checkpoint, completed.ProgressCurrent, completed.ProgressTotal, ct))
                return ContentImportProviderResult.Failure("The import lease was lost while recording activation progress.", completed.Checkpoint);
        }
        // Importers mutate the physical source behind virtual views.  Invalidation is
        // site-scoped, so this covers both views declared in this plan and already
        // provisioned views that intentionally remain outside a later replayed plan.
        await views.InvalidateAsync(context.Scope, ct);
        return completed;
    }

    private async Task<ContentImportJob?> LoadCurrentAsync(ContentImportLease lease, CancellationToken ct)
    {
        var job = await jobs.LoadAsync(lease.JobId, ct);
        return job is not null
               && job.State == ContentImportJobState.Running
               && job.LeaseExpiresOn is { } expiry && expiry > DateTimeOffset.UtcNow
               && string.Equals(job.LeaseToken, lease.Token, StringComparison.Ordinal)
               && job.FencingVersion == lease.FencingVersion
            ? job : null;
    }

    private async Task<ContentImportProviderResult> ProvisionAsync(ContentImportProvisioningPlan plan, ContentViewScope scope, CancellationToken ct)
    {
        var validation = Validate(plan, scope);
        if (validation is not null) return ContentImportProviderResult.Failure(validation, disposition: ContentImportFailureDisposition.Terminal);

        // Do every read and drift check before changing anything.  An importer must never
        // write source data only to discover that a manager has changed its code-owned CMS
        // contract, nor leave half of a plan behind because a later alias drifted.
        var missingContentTypes = new List<ContentTypeDefinition>();
        foreach (var desired in plan.ContentTypes)
        {
            var existing = await contentTypes.GetByAliasAsync(scope.SiteId, desired.Alias, ct);
            if (existing is Aero.Core.Railway.Result<ContentTypeDefinition, Aero.Core.AeroError>.Ok(var existingDefinition))
            {
                if (!Equivalent(existingDefinition, desired)) return ContentImportProviderResult.Failure($"Content type '{desired.Alias}' differs from the code-owned importer definition.", disposition: ContentImportFailureDisposition.Terminal);
                continue;
            }
            missingContentTypes.Add(desired);
        }

        var missingViews = new List<ContentSurrealViewRevision>();
        foreach (var desired in plan.Views)
        {
            var published = await views.LoadPublishedAsync(scope, desired.Alias, ct);
            if (published is not null)
            {
                if (!Equivalent(published, desired)) return ContentImportProviderResult.Failure($"Content view '{desired.Alias}' differs from the code-owned importer definition.", disposition: ContentImportFailureDisposition.Terminal);
                continue;
            }
            missingViews.Add(desired);
        }

        foreach (var desired in missingContentTypes)
        {
            var saved = await contentTypes.SaveAsync(desired, ct);
            if (saved is Aero.Core.Railway.Result<ContentTypeDefinition, Aero.Core.AeroError>.Failure(var error)) return ContentImportProviderResult.Failure($"Content type '{desired.Alias}' could not be provisioned: {error}", disposition: ContentImportFailureDisposition.Terminal);
        }
        foreach (var desired in missingViews)
        {
            var draft = desired with { Id = 0, Version = 0, PublicationState = ContentViewPublicationState.Draft };
            var saved = await views.SaveDraftAsync(draft, 100, ct);
            if (saved is null) return ContentImportProviderResult.Failure($"Content view '{desired.Alias}' could not be saved as a valid draft.", disposition: ContentImportFailureDisposition.Terminal);
            var result = await views.PublishAsync(scope, desired.Alias, saved.Version, ct);
            if (result is null) return ContentImportProviderResult.Failure($"Content view '{desired.Alias}' could not be published.", disposition: ContentImportFailureDisposition.Terminal);
        }
        return ContentImportProviderResult.Success();
    }

    private static string? Validate(ContentImportProvisioningPlan plan, ContentViewScope scope)
    {
        if (!scope.IsValid) return "The resolved site scope is invalid.";
        if (plan.ContentTypes is null || plan.Views is null) return "An importer returned an invalid provisioning plan.";
        if (plan.ContentTypes.Any(x => x is null || x.SiteId != scope.SiteId || string.IsNullOrWhiteSpace(x.Alias)))
            return "An importer attempted to provision an invalid or out-of-scope content type.";
        if (plan.Views.Any(x => x is null || x.Scope != scope || string.IsNullOrWhiteSpace(x.Alias)))
            return "An importer attempted to provision an invalid or out-of-scope content view.";
        if (plan.ContentTypes.GroupBy(x => x.Alias, StringComparer.Ordinal).Any(x => x.Count() != 1))
            return "An importer provisioning plan contains duplicate content type aliases.";
        if (plan.Views.GroupBy(x => x.Alias, StringComparer.Ordinal).Any(x => x.Count() != 1))
            return "An importer provisioning plan contains duplicate content view aliases.";
        return null;
    }

    private static bool Equivalent(ContentTypeDefinition actual, ContentTypeDefinition expected)
        => Normalize(actual, string.IsNullOrWhiteSpace(expected.ScribanTemplate)) == Normalize(expected, string.IsNullOrWhiteSpace(expected.ScribanTemplate));

    private static string Normalize(ContentTypeDefinition definition, bool generatedTemplate)
        => JsonSerializer.Serialize(new
        {
            definition.SiteId, definition.Alias, definition.Name, definition.Description, definition.Category, definition.Icon,
            definition.Cardinality, definition.Structure, definition.HierarchyRules, definition.AllowPublicUrl,
            definition.IncludeInSearch, definition.IncludeInPublicAi,
            definition.Localization,
            Fields = definition.Fields.Select(field => new
            {
                field.Name, field.FieldType, field.Label, field.Required, field.DefaultValue, field.Placeholder,
                Indexed = string.Equals(field.FieldType, "reference", StringComparison.OrdinalIgnoreCase) || field.Indexed,
                field.FullTextSearchable, field.SemanticSearchable, field.AiExposure, field.LocalizationMode, field.Settings
            }),
            ScribanTemplate = generatedTemplate ? null : definition.ScribanTemplate,
            definition.ScheduleConfig
        });
    private static bool Equivalent(ContentSurrealViewRevision actual, ContentSurrealViewRevision expected)
        => actual.Scope == expected.Scope && actual.Alias == expected.Alias && actual.ShapeAlias == expected.ShapeAlias
           && actual.ShapeFingerprint == expected.ShapeFingerprint && actual.SelectStatement == expected.SelectStatement
           && actual.IdentityField == expected.IdentityField && actual.TitleField == expected.TitleField
           && actual.EntrySelectStatement == expected.EntrySelectStatement && actual.SearchSelectStatement == expected.SearchSelectStatement
           && actual.CacheEnabled == expected.CacheEnabled && (actual.CacheDuration ?? TimeSpan.FromMinutes(5)) == (expected.CacheDuration ?? TimeSpan.FromMinutes(5))
           && actual.RelationshipId == expected.RelationshipId
           && actual.RelationshipSchemaFingerprint == expected.RelationshipSchemaFingerprint
           && actual.PublicPlanAlias == expected.PublicPlanAlias && actual.PublicPlanFingerprint == expected.PublicPlanFingerprint
           && actual.PublicPlanDialectFingerprint == expected.PublicPlanDialectFingerprint;

    private sealed class LeaseProgressSink(IContentImportJobStore jobs, ContentImportLease lease) : IContentImportProgressSink
    {
        public Task<bool> ReportAsync(string? checkpoint, long progressCurrent, long? progressTotal, CancellationToken ct = default)
            => jobs.ReportAsync(lease, checkpoint, progressCurrent, progressTotal, ct);
    }
}
