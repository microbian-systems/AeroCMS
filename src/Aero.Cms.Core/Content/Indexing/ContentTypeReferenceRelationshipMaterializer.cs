using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Infrastructure;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using SurrealDb.Net.Models;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>
/// Identity-only graph node for the CMS-owned <c>content_translation_groups</c>
/// table. The CLR name deliberately matches the physical table because Sable's
/// graph mapping resolves endpoint table names from the record type.
/// </summary>
public sealed class ContentTranslationGroups : Record;

/// <summary>
/// Native graph edge derived from a shared content-type reference. The JSON
/// field is authoritative; this row is a replaceable traversal projection.
/// </summary>
public sealed class ContentReferenceRelation : EdgeRecord
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public string RelationshipAlias { get; set; } = string.Empty;
    public string SourceContentTypeAlias { get; set; } = string.Empty;
    public string SourceFieldName { get; set; } = string.Empty;
    public long SourceTranslationGroupId { get; set; }
    public int SourceTranslationGroupRevision { get; set; }
    public string TargetContentTypeAlias { get; set; } = string.Empty;
    public long TargetTranslationGroupId { get; set; }
    public DateTimeOffset UpdatedOn { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Optimistic-concurrency boundary for incoming relationships to one content
/// translation group. It is separate from the group document so relationship
/// edits do not invalidate shared-field projection generations or editor
/// storage tokens.
/// </summary>
public sealed class ContentRelationshipTargetBarrier : SableDocument, IVersioned
{
    public long SiteId { get; set; }
    public long TranslationGroupId { get; set; }
    public long Version { get; set; }
}

/// <summary>
/// Coordinates target deletion and incoming graph mutations. New groups seed a
/// barrier in their creation transaction. Existing pre-feature groups must be
/// backfilled before they can become relationship targets.
/// </summary>
public sealed class ContentRelationshipTargetBarrierCoordinator
{
    public async Task<Result<NoneType, AeroError>> StageSourceLifecycleAsync(
        IDocumentSession session,
        ContentTranslationGroupProjectionContext context,
        CancellationToken cancellationToken = default)
    {
        var barrier = await session.LoadAsync<ContentRelationshipTargetBarrier>(
            context.TranslationGroupId,
            cancellationToken);
        if (barrier is not null && !Matches(barrier, context.SiteId, context.TranslationGroupId))
        {
            return Validation("The content relationship target barrier belongs to another site or translation group.");
        }

        if (context.Change == ContentTranslationGroupProjectionChange.Upsert)
        {
            if (barrier is null)
            {
                session.Store(new ContentRelationshipTargetBarrier
                {
                    Id = context.TranslationGroupId,
                    SiteId = context.SiteId,
                    TranslationGroupId = context.TranslationGroupId
                });
                // The pending group write is the conflict boundary while this
                // first barrier is inserted, including for a self-reference.
                context.StagedRelationshipTargetBarriers.Add(context.TranslationGroupId);
            }

            return Ok();
        }

        if (barrier is not null
            && context.StagedRelationshipTargetBarriers.Add(context.TranslationGroupId))
        {
            session.UpdateExpectedVersion(barrier, barrier.Version);
        }

        var hasInvalidScope = await session.Query<ContentReferenceRelation>()
            .Where(candidate => candidate.TargetTranslationGroupId == context.TranslationGroupId
                && candidate.SiteId != context.SiteId)
            .AnyAsync(cancellationToken);
        if (hasInvalidScope)
        {
            return Validation("An incoming content relationship has invalid site metadata.");
        }
        var hasBlockingIncoming = await session.Query<ContentReferenceRelation>()
            .Where(candidate => candidate.TargetTranslationGroupId == context.TranslationGroupId
                && candidate.SiteId == context.SiteId
                && candidate.SourceTranslationGroupId != context.TranslationGroupId)
            .AnyAsync(cancellationToken);
        if (hasBlockingIncoming)
        {
            return Prelude.Fail<NoneType, AeroError>(AeroError.ConflictError(
                "This content entry is referenced by another content entry. Remove those relationships before deleting it."));
        }
        if (barrier is null
            && await session.Query<ContentReferenceRelation>()
                .Where(candidate => candidate.TargetTranslationGroupId == context.TranslationGroupId)
                .AnyAsync(cancellationToken))
        {
            return Validation("An incoming content relationship exists without its required concurrency barrier.");
        }

        return Ok();
    }

    public async Task<Result<NoneType, AeroError>> StageTargetMutationAsync(
        IDocumentSession session,
        ContentTranslationGroupProjectionContext context,
        long targetTranslationGroupId,
        CancellationToken cancellationToken = default)
    {
        if (!context.StagedRelationshipTargetBarriers.Add(targetTranslationGroupId))
        {
            return Ok();
        }

        var barrier = await session.LoadAsync<ContentRelationshipTargetBarrier>(
            targetTranslationGroupId,
            cancellationToken);
        if (barrier is null)
        {
            return Validation(
                "The related content entry predates native relationship concurrency metadata. Backfill or recreate the target before relating it.");
        }
        if (!Matches(barrier, context.SiteId, targetTranslationGroupId))
        {
            return Validation("The related content relationship target barrier belongs to another site or translation group.");
        }

        session.UpdateExpectedVersion(barrier, barrier.Version);
        return Ok();
    }

    private static bool Matches(ContentRelationshipTargetBarrier barrier, long siteId, long translationGroupId)
        => barrier.SiteId == siteId && barrier.TranslationGroupId == translationGroupId;

    private static Result<NoneType, AeroError> Ok()
        => Prelude.Ok<NoneType, AeroError>(default);

    private static Result<NoneType, AeroError> Validation(string message)
        => Prelude.Fail<NoneType, AeroError>(AeroError.ValidationError([message]));
}

/// <summary>
/// Built-in materializer for content-type-to-content-type relationships. Both
/// endpoints are translation groups, so the relation is stable across cultures.
/// </summary>
public sealed class ContentTypeReferenceRelationshipMaterializer(
    ISelectedSiteScopeResolver? selectedSites = null,
    ContentRelationshipTargetBarrierCoordinator? targetBarriers = null)
    : IContentReferenceRelationshipMaterializer
{
    public bool CanHandle(ContentReferenceRelationshipDeclaration declaration)
        => string.Equals(
            declaration.TargetKind,
            ReferenceContentFieldSettings.TargetKindContentType,
            StringComparison.Ordinal);

    public async Task<ContentRelationshipDefinition?> DescribeAsync(
        IDocumentSession session,
        ContentViewScope scope,
        ContentReferenceRelationshipDeclaration declaration,
        CancellationToken cancellationToken = default)
    {
        if (!scope.IsValid
            || declaration.TargetContentTypeId is not { } targetContentTypeId)
        {
            return null;
        }

        var targetType = await session.LoadAsync<ContentTypeDocument>(
            targetContentTypeId,
            cancellationToken);
        if (targetType is null || targetType.SiteId != scope.SiteId)
        {
            return null;
        }

        return new ContentRelationshipDefinition(
            0,
            scope,
            declaration.Alias,
            declaration.SourceContentTypeAlias,
            targetType.Alias,
            "content_translation_groups",
            "content_translation_groups",
            null,
            null,
            "content_reference_relation",
            ContentRelationshipKind.GraphEdge,
            declaration.AllowMultiple
                ? ContentRelationshipCardinality.ManyToMany
                : ContentRelationshipCardinality.ManyToOne,
            ContentRelationshipOwnershipState.Derived,
            string.Empty);
    }

    public async Task<Result<NoneType, AeroError>> StageAsync(
        IDocumentSession session,
        ContentReferenceRelationshipProjectionContext context,
        CancellationToken cancellationToken = default)
    {
        var declaration = context.Declaration;
        if (selectedSites is null
            || targetBarriers is null
            || declaration.TargetContentTypeId is not { } targetContentTypeId)
        {
            return Validation("The content relationship cannot resolve its server-owned site scope or target content type.");
        }

        var selected = await selectedSites.ResolveAsync(
            context.TranslationGroup.SiteId,
            cancellationToken);
        if (selected is not { IsValid: true } scope
            || scope.SiteId != context.TranslationGroup.SiteId)
        {
            return Validation("The content relationship cannot resolve its authoritative tenant and site scope.");
        }

        var targetType = await session.LoadAsync<ContentTypeDocument>(targetContentTypeId, cancellationToken);
        if (targetType is null || targetType.SiteId != scope.SiteId)
        {
            return Validation("The related content type no longer exists in this site.");
        }

        // Source group identity and the code-owned declaration alias are authoritative lookup
        // anchors. Do not prefilter tenant/site metadata: a poisoned row must be observed and
        // rejected rather than made invisible and replaced with a duplicate.
        var existing = await session.Query<ContentReferenceRelation>()
            .Where(candidate => candidate.SourceTranslationGroupId == context.TranslationGroup.TranslationGroupId
                && candidate.RelationshipAlias == declaration.Alias)
            .ToListAsync(cancellationToken);
        if ((!declaration.AllowMultiple && existing.Count > 1)
            || existing.Select(candidate => candidate.TargetTranslationGroupId).Distinct().Count() != existing.Count
            || existing.Any(candidate => IsCorruptedExistingRelationship(
                candidate,
                scope,
                context.TranslationGroup.TranslationGroupId,
                declaration,
                targetType.Alias)))
        {
            return Validation("The existing native content relationship is corrupted or belongs to another declaration.");
        }

        foreach (var existingTargetGroupId in existing
                     .Select(candidate => candidate.TargetTranslationGroupId)
                     .Distinct())
        {
            var barrierResult = await targetBarriers.StageTargetMutationAsync(
                session,
                context.TranslationGroup,
                existingTargetGroupId,
                cancellationToken);
            if (barrierResult is Result<NoneType, AeroError>.Failure)
            {
                return barrierResult;
            }
        }

        if (context.TranslationGroup.Change == ContentTranslationGroupProjectionChange.Delete
            || context.Value is null
            || context.Value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            QueueDelete(session, scope, context, declaration);
            return Ok();
        }

        var ids = ReadTargetIds(context.Value.Value, declaration.AllowMultiple);
        if (ids is null)
        {
            return Validation("The related content value does not match its declared cardinality.");
        }

        var resolvedTargets = new List<long>();
        foreach (var targetItemId in ids.Distinct())
        {
            var targetItem = await session.LoadAsync<ContentItem>(targetItemId, cancellationToken);
            if (targetItem is null
                || targetItem.SiteId != scope.SiteId
                || !string.Equals(targetItem.ContentTypeAlias, targetType.Alias, StringComparison.Ordinal)
                || targetItem.TranslationGroupId is not { } targetGroupId)
            {
                return Validation("The related content entry no longer resolves to the declared target content type and site.");
            }


            var barrierResult = await targetBarriers.StageTargetMutationAsync(
                session,
                context.TranslationGroup,
                targetGroupId,
                cancellationToken);
            if (barrierResult is Result<NoneType, AeroError>.Failure)
            {
                return barrierResult;
            }

            var targetGroup = await session.LoadAsync<ContentTranslationGroupDocument>(
                targetGroupId,
                cancellationToken);
            if (targetGroup is null
                || targetGroup.SiteId != scope.SiteId
                || !string.Equals(targetGroup.ContentTypeAlias, targetType.Alias, StringComparison.Ordinal))
            {
                return Validation("The related content translation group no longer matches the declared target content type and site.");
            }
            resolvedTargets.Add(targetGroupId);
        }

        // Sable executes queued SQL in order within the same transaction as
        // the canonical content/group write. Delete the previous projection,
        // then create the complete replacement set. Values are parameters;
        // only CMS-owned table and field names appear in the statement.
        QueueDelete(session, scope, context, declaration);
        foreach (var targetGroupId in resolvedTargets.Distinct())
        {
            QueueRelate(session, scope, context, declaration, targetType.Alias, targetGroupId);
        }

        return Ok();
    }

    private static bool IsCorruptedExistingRelationship(
        ContentReferenceRelation candidate,
        SelectedSiteScope scope,
        long expectedSourceTranslationGroupId,
        ContentReferenceRelationshipDeclaration declaration,
        string expectedTargetContentTypeAlias)
    {
        if (candidate.TenantId != scope.TenantId
            || candidate.SiteId != scope.SiteId
            || candidate.In is null
            || candidate.Out is null
            || !string.Equals(candidate.In.Table, "content_translation_groups", StringComparison.Ordinal)
            || !TryReadLongId(candidate.In, out var sourceTranslationGroupId)
            || sourceTranslationGroupId != expectedSourceTranslationGroupId
            || candidate.SourceTranslationGroupId != expectedSourceTranslationGroupId
            || !string.Equals(candidate.Out.Table, "content_translation_groups", StringComparison.Ordinal)
            || !TryReadLongId(candidate.Out, out var targetTranslationGroupId)
            || targetTranslationGroupId <= 0
            || candidate.TargetTranslationGroupId != targetTranslationGroupId
            || !string.Equals(candidate.RelationshipAlias, declaration.Alias, StringComparison.Ordinal)
            || !string.Equals(candidate.TargetContentTypeAlias, expectedTargetContentTypeAlias, StringComparison.Ordinal)
            || !string.Equals(candidate.SourceContentTypeAlias, declaration.SourceContentTypeAlias, StringComparison.Ordinal)
            || !string.Equals(candidate.SourceFieldName, declaration.SourceFieldName, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool TryReadLongId(RecordId recordId, out long id)
    {
        if (recordId.TryDeserializeId<long>(out id))
        {
            return true;
        }

        if (recordId.TryDeserializeId<int>(out var integer))
        {
            id = integer;
            return true;
        }

        if (recordId.TryDeserializeId<ulong>(out var unsigned)
            && unsigned <= long.MaxValue)
        {
            id = (long)unsigned;
            return true;
        }

        if (recordId.TryDeserializeId<string>(out var text)
            && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            return true;
        }

        id = 0;
        return false;
    }

    private static IReadOnlyList<long>? ReadTargetIds(JsonElement value, bool allowMultiple)
    {
        if (!allowMultiple)
        {
            return TryReadId(value, out var id) ? [id] : null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<long>();
        foreach (var item in value.EnumerateArray())
        {
            if (!TryReadId(item, out var id))
            {
                return null;
            }
            values.Add(id);
        }
        return values;
    }

    private static bool TryReadId(JsonElement value, out long id)
    {
        id = 0;
        return value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out id)
            && id > 0;
    }

    private static void QueueDelete(
        IDocumentSession session,
        SelectedSiteScope scope,
        ContentReferenceRelationshipProjectionContext context,
        ContentReferenceRelationshipDeclaration declaration)
    {
        session.QueueSqlCommand(
            "{database}",
            "DELETE `content_reference_relation` " +
            "WHERE tenant_id = $p0 AND site_id = $p1 " +
            "AND source_translation_group_id = $p2 AND relationship_alias = $p3;",
            scope.TenantId,
            scope.SiteId,
            context.TranslationGroup.TranslationGroupId,
            declaration.Alias);
    }

    private static void QueueRelate(
        IDocumentSession session,
        SelectedSiteScope scope,
        ContentReferenceRelationshipProjectionContext context,
        ContentReferenceRelationshipDeclaration declaration,
        string targetContentTypeAlias,
        long targetTranslationGroupId)
    {
        session.QueueSqlCommand(
            "{database}",
            "RELATE $p0 -> `content_reference_relation` -> $p1 SET " +
            "tenant_id = $p2, site_id = $p3, relationship_alias = $p4, " +
            "source_content_type_alias = $p5, source_field_name = $p6, " +
            "source_translation_group_id = $p7, source_translation_group_revision = $p8, " +
            "target_content_type_alias = $p9, target_translation_group_id = $p10, updated_on = $p11;",
            new RecordIdOf<long>("content_translation_groups", context.TranslationGroup.TranslationGroupId),
            new RecordIdOf<long>("content_translation_groups", targetTranslationGroupId),
            scope.TenantId,
            scope.SiteId,
            declaration.Alias,
            declaration.SourceContentTypeAlias,
            declaration.SourceFieldName,
            context.TranslationGroup.TranslationGroupId,
            context.TranslationGroup.Revision,
            targetContentTypeAlias,
            targetTranslationGroupId,
            DateTimeOffset.UtcNow);
    }

    private static Result<NoneType, AeroError> Ok()
        => Prelude.Ok<NoneType, AeroError>(default);

    private static Result<NoneType, AeroError> Validation(string message)
        => Prelude.Fail<NoneType, AeroError>(AeroError.ValidationError([message]));
}
