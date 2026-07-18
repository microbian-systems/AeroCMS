using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Creates detached snapshots for FusionCache. Cached document instances must
/// never be handed directly to callers because the domain models are mutable.
/// </summary>
internal static class ContentCacheSnapshot
{
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
            ModifiedBy = source.ModifiedBy
        };

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
            AllowPublicUrl = source.AllowPublicUrl,
            HideFromSearch = source.HideFromSearch,
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

    private static ContentFieldDefinition Clone(ContentFieldDefinition source) =>
        new()
        {
            Name = source.Name,
            FieldType = source.FieldType,
            Label = source.Label,
            Required = source.Required,
            DefaultValue = source.DefaultValue,
            Placeholder = source.Placeholder,
            Settings = source.Settings.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Clone(),
                source.Settings.Comparer)
        };
}
