using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;

namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Creates detached snapshots for FusionCache. Cached document instances must
/// never be handed directly to callers because the domain models are mutable.
/// </summary>
internal static class ContentCacheSnapshot
{
    /// <summary>
    /// Deep-copies a content item, including cloned JSON field values.
    /// </summary>
    /// <param name="source">The mutable source item.</param>
    /// <returns>A detached item preserving the source dictionary comparer.</returns>
    public static ContentItem Clone(ContentItem source) =>
        new()
        {
            Id = source.Id,
            SiteId = source.SiteId,
            ContentTypeAlias = source.ContentTypeAlias,
            Slug = source.Slug,
            Title = source.Title,
            TranslationGroupId = source.TranslationGroupId,
            Culture = source.Culture,
            SourceItemId = source.SourceItemId,
            ParentId = source.ParentId,
            SortOrder = source.SortOrder,
            Fields = source.Fields.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Clone(),
                source.Fields.Comparer),
            PublicationState = source.PublicationState,
            PublishedOn = source.PublishedOn,
            VersionNumber = source.VersionNumber,
            SchedulePublishUtc = source.SchedulePublishUtc,
            ScheduleUnpublishUtc = source.ScheduleUnpublishUtc,
            CreatedOn = source.CreatedOn,
            ModifiedOn = source.ModifiedOn,
            CreatedBy = source.CreatedBy,
            ModifiedBy = source.ModifiedBy,
            Version = source.Version,
            TranslationProvenance = source.TranslationProvenance,
            TranslationReview = Clone(source.TranslationReview)
        };

    /// <summary>
    /// Deep-copies a content-type definition, its field definitions, settings, and schedule record.
    /// </summary>
    public static ContentTypeDefinition Clone(ContentTypeDefinition source) =>
        new()
        {
            Id = source.Id,
            SiteId = source.SiteId,
            Alias = source.Alias,
            Name = source.Name,
            Description = source.Description,
            Category = source.Category,
            Icon = source.Icon,
            Cardinality = source.Cardinality,
            Structure = source.Structure,
            HierarchyRules = source.HierarchyRules with
            {
                AllowedParentContentTypeIds =
                    source.HierarchyRules.AllowedParentContentTypeIds.ToArray()
            },
            AllowPublicUrl = source.AllowPublicUrl,
            IncludeInSearch = source.IncludeInSearch,
            IncludeInPublicAi = source.IncludeInPublicAi,
            Localization = new()
            {
                CultureFallbackPolicy = source.Localization.CultureFallbackPolicy,
                AiTranslationReviewPolicy = source.Localization.AiTranslationReviewPolicy
            },
            Fields = source.Fields.Select(Clone).ToList(),
            ScribanTemplate = source.ScribanTemplate,
            ScheduleConfig = source.ScheduleConfig is null
                ? null
                : source.ScheduleConfig with { },
            CreatedOn = source.CreatedOn,
            ModifiedOn = source.ModifiedOn,
            CreatedBy = source.CreatedBy,
            ModifiedBy = source.ModifiedBy
        };

    /// <summary>
    /// Deep-copies one field definition and its JSON settings.
    /// </summary>
    private static ContentFieldDefinition Clone(ContentFieldDefinition source) =>
        new()
        {
            Name = source.Name,
            FieldType = source.FieldType,
            Label = source.Label,
            Required = source.Required,
            DefaultValue = source.DefaultValue,
            Placeholder = source.Placeholder,
            Indexed = source.Indexed,
            FullTextSearchable = source.FullTextSearchable,
            SemanticSearchable = source.SemanticSearchable,
            AiExposure = source.AiExposure,
            LocalizationMode = source.LocalizationMode,
            Settings = source.Settings.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Clone(),
                source.Settings.Comparer)
        };

    private static ContentTranslationReview Clone(ContentTranslationReview source) =>
        new(source.Status, source.ReviewedOn, source.ReviewedBy, source.Notes,
            source.ReviewedSourceItemId, source.ReviewedSourceVersionNumber, source.ReviewedTargetVersionNumber);
}
