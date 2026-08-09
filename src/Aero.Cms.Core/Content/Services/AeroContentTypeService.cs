using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Templating;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using System.Globalization;
using System.Text.Json;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Implements <see cref="IContentTypeService"/> with a Sable document session.
/// </summary>
public sealed class AeroContentTypeService(
    IDocumentSession session,
    IEnumerable<IFieldTemplateSnippet> snippets,
    ScribanTemplateValidator templateValidator) : IContentTypeService
{
    /// <inheritdoc />
    public async Task<Result<ContentTypeDefinition, AeroError>> GetByIdAsync(
        long siteId,
        long id,
        CancellationToken ct = default)
    {
        var doc = await session.LoadAsync<ContentTypeDocument>(id, ct);
        return doc is null || doc.SiteId != siteId
            ? Prelude.Fail<ContentTypeDefinition, AeroError>(
                AeroError.NotFoundError($"Content type '{id}' not found."))
            : Prelude.Ok<ContentTypeDefinition, AeroError>(Map(doc));
    }

    /// <inheritdoc />
    public async Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default)
    {
        var doc = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Alias == alias, ct);

        if (doc is null)
            return Prelude.Fail<ContentTypeDefinition, AeroError>(AeroError.NotFoundError($"Content type '{alias}' not found."));
        return Prelude.Ok<ContentTypeDefinition, AeroError>(Map(doc));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(long siteId, CancellationToken ct = default)
    {
        var docs = await session.Query<ContentTypeDocument>().Where(x => x.SiteId == siteId).ToListAsync(ct);
        return Prelude.Ok<IReadOnlyList<ContentTypeDefinition>, AeroError>(docs.Select(Map).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<ContentTypeDefinition, AeroError>> SaveAsync(ContentTypeDefinition definition, CancellationToken ct = default)
    {
        var fieldValidation = CompositeContentFieldDefinitionValidator.Validate(definition.Fields);
        if (fieldValidation is Result<NoneType, AeroError>.Failure fieldFailure)
        {
            return fieldFailure.Error;
        }

        var scalarFieldValidation =
            ScalarContentFieldDefinitionValidator.Validate(definition.Fields);
        if (scalarFieldValidation is Result<NoneType, AeroError>.Failure scalarFieldFailure)
        {
            return scalarFieldFailure.Error;
        }

        foreach (var field in definition.Fields.Where(
                     field => field.FieldType == ContentFieldTypes.Reference))
        {
            field.Indexed = true;
        }

        var searchFieldValidation =
            ContentFieldSearchDefinitionValidator.Validate(definition.Fields);
        if (searchFieldValidation is Result<NoneType, AeroError>.Failure searchFieldFailure)
        {
            return searchFieldFailure.Error;
        }

        var referenceValidation = await ValidateReferenceFieldsAsync(definition, ct);
        if (referenceValidation is Result<NoneType, AeroError>.Failure referenceFailure)
        {
            return referenceFailure.Error;
        }

        var hierarchyValidation = await ValidateHierarchyAsync(definition, ct);
        if (hierarchyValidation is Result<NoneType, AeroError>.Failure hierarchyFailure)
        {
            return hierarchyFailure.Error;
        }

        ContentTypeDocument? stored = null;
        if (definition.Id != 0)
        {
            stored = await session.LoadAsync<ContentTypeDocument>(definition.Id, ct);
            if (stored is null || stored.SiteId != definition.SiteId)
                return Prelude.Fail<ContentTypeDefinition, AeroError>(AeroError.NotFoundError($"Content type '{definition.Id}' not found."));

            if (!string.Equals(stored.Alias, definition.Alias, StringComparison.Ordinal))
            {
                return Prelude.Fail<ContentTypeDefinition, AeroError>(
                    AeroError.ConflictError(
                        "Changing a content-type alias requires an explicit conversion workflow."));
            }
        }

        var existing = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == definition.SiteId && x.Alias == definition.Alias, ct);

        if (existing is not null && existing.Id != definition.Id)
        {
            return Prelude.Fail<ContentTypeDefinition, AeroError>(
                AeroError.CreateError($"A content type with alias '{definition.Alias}' already exists for this site."));
        }

        var templateValidation = PrepareTemplate(definition);
        if (templateValidation is Result<NoneType, AeroError>.Failure templateFailure)
            return templateFailure.Error;

        var doc = new ContentTypeDocument
        {
            Id = stored?.Id ?? Snowflake.NewId(),
            SiteId = stored?.SiteId ?? definition.SiteId,
            Alias = definition.Alias,
            Name = definition.Name,
            Description = definition.Description,
            Category = definition.Category,
            Icon = definition.Icon,
            Cardinality = definition.Cardinality,
            Structure = definition.Structure,
            HierarchyRules = definition.HierarchyRules,
            AllowPublicUrl = definition.AllowPublicUrl,
            IncludeInSearch = definition.IncludeInSearch,
            IncludeInPublicAi = definition.IncludeInPublicAi,
            Fields = definition.Fields,
            ScribanTemplate = definition.ScribanTemplate,
            ScheduleConfig = definition.ScheduleConfig
        };

        session.Store(doc);
        await session.SaveChangesAsync(ct);
        definition.Id = doc.Id;
        return Prelude.Ok<ContentTypeDefinition, AeroError>(definition);
    }

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> DeleteAsync(long siteId, string alias, CancellationToken ct = default)
    {
        var doc = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Alias == alias, ct);

        if (doc is null)
            return Prelude.Ok<bool, AeroError>(false);

        var dependents = await session.Query<ContentTypeDocument>()
            .Where(candidate => candidate.SiteId == siteId && candidate.Id != doc.Id)
            .ToListAsync(ct);
        if (dependents.Any(candidate =>
                candidate.HierarchyRules.AllowedParentContentTypeIds.Contains(doc.Id)
                || candidate.Fields.Any(field =>
                    field.FieldType == ContentFieldTypes.Reference
                    && (!field.Settings.TryGetValue(ReferenceContentFieldSettings.TargetContentTypeId, out _)
                        || !TryGetTargetContentTypeId(field, out var targetId)
                        || targetId == doc.Id))))
        {
            return Prelude.Fail<bool, AeroError>(AeroError.ConflictError(
                "This content type is referenced by another content type."));
        }

        session.Delete(doc);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private static ContentTypeDefinition Map(ContentTypeDocument doc) => new()
    {
        Id = doc.Id, SiteId = doc.SiteId, Alias = doc.Alias, Name = doc.Name, Description = doc.Description,
        Category = doc.Category, Icon = doc.Icon, Cardinality = doc.Cardinality, Structure = doc.Structure,
        HierarchyRules = doc.HierarchyRules, AllowPublicUrl = doc.AllowPublicUrl,
        IncludeInSearch = doc.IncludeInSearch, IncludeInPublicAi = doc.IncludeInPublicAi, Fields = doc.Fields,
        ScribanTemplate = doc.ScribanTemplate, ScheduleConfig = doc.ScheduleConfig
    };

    private Result<NoneType, AeroError> PrepareTemplate(ContentTypeDefinition definition)
    {
        var template = string.IsNullOrWhiteSpace(definition.ScribanTemplate)
            ? ContentTypeTemplateGenerator.GenerateTemplate(definition, snippets)
            : definition.ScribanTemplate;
        template = ContentTypeTemplateGenerator.NormalizeTemplate(template, definition.Fields);
        definition.ScribanTemplate = template;
        using var schema = ContentTypeSchemaGenerator.GenerateSchema(definition);

        var validation = templateValidator.Validate(template, schema);
        if (validation is Result<NoneType, AeroError>.Failure)
            return validation;
        return Prelude.Ok<NoneType, AeroError>(Prelude.None);
    }

    private async Task<Result<NoneType, AeroError>> ValidateHierarchyAsync(
        ContentTypeDefinition definition,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(definition.Cardinality))
        {
            return AeroError.ValidationError(["The content cardinality is invalid."]);
        }

        if (!Enum.IsDefined(definition.Structure))
        {
            return AeroError.ValidationError(["The content structure is invalid."]);
        }

        if (definition.Structure == ContentStructure.Hierarchical
            && definition.Cardinality == ContentCardinality.Singleton)
        {
            return AeroError.ValidationError(
                ["Hierarchical content types must use collection cardinality because a hierarchy contains multiple entries."]);
        }

        if (definition.HierarchyRules is null)
        {
            return AeroError.ValidationError(["Content hierarchy rules are required."]);
        }

        if (definition.Structure != ContentStructure.Hierarchical)
        {
            return Prelude.Ok<NoneType, AeroError>(Prelude.None);
        }

        var rules = definition.HierarchyRules;
        if (rules.MaximumDepth is < 1 or > ContentHierarchyValidator.MaximumSystemDepth)
        {
            return AeroError.ValidationError(
                [$"Hierarchy maximum depth must be between 1 and {ContentHierarchyValidator.MaximumSystemDepth}."]);
        }

        if (rules.AllowedParentContentTypeIds.Any(id => id <= 0))
        {
            return AeroError.ValidationError(
                ["Allowed parent content-type identifiers must be positive."]);
        }

        foreach (var parentTypeId in rules.AllowedParentContentTypeIds.Distinct())
        {
            if (definition.Id > 0 && parentTypeId == definition.Id)
                continue;

            var parent = await session.LoadAsync<ContentTypeDocument>(parentTypeId, ct);
            if (parent is null || parent.SiteId != definition.SiteId)
            {
                return AeroError.ValidationError(
                    ["Allowed parent content types must exist in the current site."]);
            }
        }

        if (!string.Equals(
                rules.DefaultOrdering,
                "sortOrder,title",
                StringComparison.OrdinalIgnoreCase))
        {
            return AeroError.ValidationError(
                ["The supported hierarchy ordering is 'sortOrder,title'."]);
        }

        return Prelude.Ok<NoneType, AeroError>(Prelude.None);
    }

    private async Task<Result<NoneType, AeroError>> ValidateReferenceFieldsAsync(
        ContentTypeDefinition definition,
        CancellationToken ct)
    {
        var errors = new List<string>();

        foreach (var field in definition.Fields.Where(
                     field => field.FieldType == ContentFieldTypes.Reference))
        {
            var label = field.Label ?? field.Name;
            if (ReferenceFieldValidator.IsCmsDocumentReference(field))
            {
                ValidateCmsDocumentReferenceDefinition(field, label, errors);
                continue;
            }

            if (!TryGetTargetContentTypeId(field, out var targetId))
            {
                errors.Add(
                    $"Reference field '{label}' must select a positive content-type identifier.");
                continue;
            }

            ContentStructure targetStructure;
            IReadOnlyList<ContentFieldDefinition> targetFields;
            if (definition.Id > 0 && targetId == definition.Id)
            {
                targetStructure = definition.Structure;
                targetFields = definition.Fields;
            }
            else
            {
                var target = await session.LoadAsync<ContentTypeDocument>(targetId, ct);
                if (target is not null && target.SiteId != definition.SiteId)
                    target = null;
                if (target is null)
                {
                    errors.Add(
                        $"Reference field '{label}' targets an unknown content type '{targetId.ToString(CultureInfo.InvariantCulture)}'.");
                    continue;
                }

                targetStructure = target.Structure;
                targetFields = target.Fields;
            }

            if (IsHierarchyReference(field)
                && targetStructure != ContentStructure.Hierarchical)
            {
                errors.Add(
                    $"Hierarchy reference field '{label}' must target a hierarchical content type.");
            }

            var dependsOnField = GetStringSetting(
                field,
                ReferenceContentFieldSettings.DependsOnField);
            var targetFilterField = GetStringSetting(
                field,
                ReferenceContentFieldSettings.TargetFilterField);
            if (string.IsNullOrWhiteSpace(dependsOnField)
                && string.IsNullOrWhiteSpace(targetFilterField))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(dependsOnField)
                || string.IsNullOrWhiteSpace(targetFilterField))
            {
                errors.Add(
                    $"Reference field '{label}' must configure both its dependent field and target relationship field.");
                continue;
            }

            var dependency = definition.Fields.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Name,
                    dependsOnField,
                    StringComparison.Ordinal));
            if (dependency is null
                || dependency.FieldType != ContentFieldTypes.Reference)
            {
                errors.Add(
                    $"Reference field '{label}' depends on an unknown or non-reference field '{dependsOnField}'.");
                continue;
            }

            var targetRelationship = targetFields.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Name,
                    targetFilterField,
                    StringComparison.Ordinal));
            if (targetRelationship is null
                || targetRelationship.FieldType != ContentFieldTypes.Reference)
            {
                errors.Add(
                    $"Reference field '{label}' filters by an unknown or non-reference target field '{targetFilterField}'.");
                continue;
            }

            if (!TryGetTargetContentTypeId(dependency, out var dependencyTarget)
                || !TryGetTargetContentTypeId(targetRelationship, out var relationshipTarget)
                || dependencyTarget != relationshipTarget)
            {
                errors.Add(
                    $"Reference field '{label}' cannot cascade because '{dependsOnField}' and its target relationship field target different content types.");
            }
        }

        return errors.Count == 0
            ? Prelude.Ok<NoneType, AeroError>(Prelude.None)
            : AeroError.ValidationError(errors);
    }

    private static bool TryGetTargetContentTypeId(
        ContentFieldDefinition field,
        out long id)
    {
        id = 0;
        return field.Settings.TryGetValue(
                   ReferenceContentFieldSettings.TargetContentTypeId,
                   out var value)
               && value.ValueKind == JsonValueKind.String
               && long.TryParse(
                   value.GetString(),
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out id)
               && id > 0;
    }

    private static void ValidateCmsDocumentReferenceDefinition(
        ContentFieldDefinition field,
        string label,
        List<string> errors)
    {
        var sources = ReferenceFieldValidator.GetAllowedSources(field);
        if (sources.Count == 0)
        {
            errors.Add(
                $"Reference field '{label}' must allow at least one CMS content source.");
        }
        else
        {
            var unsupported = sources
                .Where(source =>
                    !CmsContentReferenceSources.All.Contains(source))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (unsupported.Length > 0)
            {
                errors.Add(
                    $"Reference field '{label}' contains unsupported sources: {string.Join(", ", unsupported)}.");
            }
        }

        if (IsHierarchyReference(field)
            || !string.IsNullOrWhiteSpace(
                GetStringSetting(
                    field,
                    ReferenceContentFieldSettings.DependsOnField))
            || !string.IsNullOrWhiteSpace(
                GetStringSetting(
                    field,
                    ReferenceContentFieldSettings.TargetFilterField)))
        {
            errors.Add(
                $"Reference field '{label}' cannot combine CMS documents with hierarchy or cascading settings.");
        }
    }

    private static bool IsHierarchyReference(ContentFieldDefinition field) =>
        field.FieldType == ContentFieldTypes.Reference
        && string.Equals(
            GetStringSetting(field, ReferenceContentFieldSettings.SelectionMode),
            ReferenceContentFieldSettings.SelectionModeHierarchy,
            StringComparison.Ordinal);

    private static string? GetStringSetting(ContentFieldDefinition field, string key) =>
        field.Settings.TryGetValue(key, out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;
}
