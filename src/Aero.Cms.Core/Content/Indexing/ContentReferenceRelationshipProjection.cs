using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>
/// A content-type-owned declaration that projects one canonical reference field
/// into a native database relationship.
/// </summary>
public sealed record ContentReferenceRelationshipDeclaration(
    string Alias,
    string SourceContentTypeAlias,
    string SourceFieldName,
    string TargetKind,
    long? TargetContentTypeId,
    IReadOnlyList<string> AllowedProviders,
    bool AllowMultiple)
{
    public static bool TryCreate(
        string sourceContentTypeAlias,
        ContentFieldDefinition field,
        out ContentReferenceRelationshipDeclaration? declaration)
    {
        declaration = null;
        if (field.FieldType != ContentFieldTypes.Reference
            || field.LocalizationMode != ContentFieldLocalizationMode.Shared
            || !TryGetString(field, ReferenceContentFieldSettings.RelationshipAlias, out var alias))
        {
            return false;
        }

        var targetKind = TryGetString(field, ReferenceContentFieldSettings.TargetKind, out var configuredKind)
            ? configuredKind
            : ReferenceContentFieldSettings.TargetKindContentType;
        long? targetContentTypeId = null;
        if (TryGetString(field, ReferenceContentFieldSettings.TargetContentTypeId, out var targetIdText)
            && long.TryParse(targetIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var targetId)
            && targetId > 0)
        {
            targetContentTypeId = targetId;
        }

        var providers = field.Settings.TryGetValue(ReferenceContentFieldSettings.AllowedProviders, out var providerSetting)
            && providerSetting.ValueKind == JsonValueKind.Array
                ? providerSetting.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()?.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
                : [];
        var allowMultiple = field.Settings.TryGetValue(ReferenceContentFieldSettings.AllowMultiple, out var multiple)
            && multiple.ValueKind == JsonValueKind.True;

        declaration = new(
            alias,
            sourceContentTypeAlias,
            field.Name,
            targetKind,
            targetContentTypeId,
            providers,
            allowMultiple);
        return true;
    }

    private static bool TryGetString(ContentFieldDefinition field, string key, out string value)
    {
        value = string.Empty;
        if (!field.Settings.TryGetValue(key, out var setting)
            || setting.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(setting.GetString()))
        {
            return false;
        }

        value = setting.GetString()!.Trim();
        return true;
    }
}

/// <summary>
/// Exact, server-owned input for one native relationship materializer.
/// </summary>
public sealed record ContentReferenceRelationshipProjectionContext(
    ContentTranslationGroupProjectionContext TranslationGroup,
    ContentReferenceRelationshipDeclaration Declaration,
    ContentFieldDefinition Field,
    JsonElement? Value);

/// <summary>
/// Host-neutral extension point for a physical relationship representation.
/// Implementations validate and queue changes on the caller-owned session and
/// must never commit independently.
/// </summary>
public interface IContentReferenceRelationshipMaterializer
{
    bool CanHandle(ContentReferenceRelationshipDeclaration declaration);

    /// <summary>
    /// Describes the code-owned physical representation for relationship
    /// inventory and adoption. The catalog validates and fingerprints the
    /// returned descriptor; implementations must not persist metadata here.
    /// </summary>
    Task<ContentRelationshipDefinition?> DescribeAsync(
        IDocumentSession session,
        ContentViewScope scope,
        ContentReferenceRelationshipDeclaration declaration,
        CancellationToken cancellationToken = default);

    Task<Result<NoneType, AeroError>> StageAsync(
        IDocumentSession session,
        ContentReferenceRelationshipProjectionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Finds relationship declarations in the saved content type and dispatches
/// each one to exactly one registered materializer in the same unit of work as
/// the translation-group mutation.
/// </summary>
public sealed class ContentReferenceRelationshipProjectionContributor(
    IEnumerable<IContentReferenceRelationshipMaterializer> materializers,
    ContentRelationshipTargetBarrierCoordinator targetBarriers)
    : IContentTranslationGroupProjectionContributor
{
    private readonly IReadOnlyList<IContentReferenceRelationshipMaterializer> _materializers =
        materializers.ToArray();

    public async Task<Result<NoneType, AeroError>> StageAsync(
        IDocumentSession session,
        ContentTranslationGroupProjectionContext context,
        CancellationToken cancellationToken = default)
    {
        var lifecycleResult = await targetBarriers.StageSourceLifecycleAsync(
            session,
            context,
            cancellationToken);
        if (lifecycleResult is Result<NoneType, AeroError>.Failure)
        {
            return lifecycleResult;
        }

        var contentType = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(candidate => candidate.SiteId == context.SiteId
                && candidate.Alias == context.ContentTypeAlias, cancellationToken);
        if (contentType is null)
        {
            return Prelude.Fail<NoneType, AeroError>(
                AeroError.NotFoundError("The relationship source content type was not found."));
        }

        foreach (var field in contentType.Fields)
        {
            if (!ContentReferenceRelationshipDeclaration.TryCreate(contentType.Alias, field, out var declaration)
                || declaration is null)
            {
                continue;
            }

            var handlers = _materializers.Where(candidate => candidate.CanHandle(declaration)).ToArray();
            if (handlers.Length != 1)
            {
                return Prelude.Fail<NoneType, AeroError>(AeroError.ValidationError(
                    [$"Relationship '{declaration.Alias}' requires exactly one registered native materializer; found {handlers.Length}."]));
            }

            JsonElement? value = null;
            if (context.Change != ContentTranslationGroupProjectionChange.Delete
                && context.SharedFields.TryGetValue(field.Name, out var storedValue))
            {
                value = storedValue.Clone();
            }

            var result = await handlers[0].StageAsync(
                session,
                new ContentReferenceRelationshipProjectionContext(context, declaration, field, value),
                cancellationToken);
            if (result is Result<NoneType, AeroError>.Failure)
            {
                return result;
            }
        }

        return Prelude.Ok<NoneType, AeroError>(default);
    }
}
