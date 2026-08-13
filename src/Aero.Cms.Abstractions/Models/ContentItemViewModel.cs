using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Orleans-serializable viewmodel for content items.
/// </summary>
[Alias("ContentItemViewModel")]
[GenerateSerializer]
public sealed record ContentItemViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Content Type Alias.
    /// </summary>
[Id(0)]
    public string ContentTypeAlias { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[Id(1)]
    public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
[Id(2)]
    public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Fields Json.
    /// </summary>
[Id(3)]
    public string FieldsJson { get; set; } = "{}";
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
[Id(4)]
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
[Id(5)]
    public DateTimeOffset? PublishedOn { get; set; }
        /// <summary>
    /// Gets or sets the Version Number.
    /// </summary>
[Id(6)]
    public int VersionNumber { get; set; }
        /// <summary>
    /// Gets or sets the Schedule Publish Utc.
    /// </summary>
[Id(7)]
    public DateTimeOffset? SchedulePublishUtc { get; set; }
        /// <summary>
    /// Gets or sets the Schedule Unpublish Utc.
    /// </summary>
[Id(8)]
    public DateTimeOffset? ScheduleUnpublishUtc { get; set; }
        /// <summary>
    /// Gets or sets the Translation Group Id.
    /// </summary>
[Id(9)]
    public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
[Id(10)]
    public string Culture { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Source Item Id.
    /// </summary>
    [Id(11)]
    public long? SourceItemId { get; set; }

    /// <summary>Gets or sets the source-of-truth parent item identifier.</summary>
    [Id(12)]
    public long? ParentId { get; set; }

    /// <summary>Gets or sets the stable order among siblings.</summary>
    [Id(13)]
    public int SortOrder { get; set; }

    /// <summary>Serialized immutable translation provenance preserved across ordinary manager edits.</summary>
    [Id(14)]
    public string? TranslationProvenanceJson { get; set; }

    /// <summary>Serialized translation review preserved across ordinary manager edits.</summary>
    [Id(15)]
    public string? TranslationReviewJson { get; set; }

    /// <summary>Current revision of the owning translation group, when resolved.</summary>
    [Id(16)]
    public int? TranslationGroupRevision { get; set; }

    [Id(17)] public long StorageVersion { get; set; }
    [Id(18)] public long? TranslationGroupStorageVersion { get; set; }
}

/// <summary>
/// Represents a record for ContentItemErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("ContentItemErrorViewModel")]
public record ContentItemErrorViewModel : AeroErrorViewModel<ContentItemViewModel>;
