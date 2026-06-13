using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Orleans-serializable viewmodel for content items.
/// </summary>
[Alias("ContentItemViewModel")]
[GenerateSerializer]
public sealed record ContentItemViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string ContentTypeAlias { get; set; } = string.Empty;
    [Id(1)]
    public string Slug { get; set; } = string.Empty;
    [Id(2)]
    public string? Title { get; set; }
    [Id(3)]
    public string FieldsJson { get; set; } = "{}";
    [Id(4)]
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    [Id(5)]
    public DateTimeOffset? PublishedOn { get; set; }
    [Id(6)]
    public int VersionNumber { get; set; }
    [Id(7)]
    public DateTimeOffset? SchedulePublishUtc { get; set; }
    [Id(8)]
    public DateTimeOffset? ScheduleUnpublishUtc { get; set; }
    [Id(9)]
    public long? TranslationGroupId { get; set; }
    [Id(10)]
    public string Culture { get; set; } = string.Empty;
    [Id(11)]
    public long? SourceItemId { get; set; }
}

[GenerateSerializer]
[Alias("ContentItemErrorViewModel")]
public record ContentItemErrorViewModel : AeroErrorViewModel<ContentItemViewModel>;
