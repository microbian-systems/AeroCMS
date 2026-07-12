using Aero.Cms.Abstractions.Enums;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Represents a class for ContentItem.
/// </summary>
public sealed class ContentItem : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Content Type Alias.
    /// </summary>
public string ContentTypeAlias { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Translation Group Id.
    /// </summary>
public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Source Item Id.
    /// </summary>
public long? SourceItemId { get; set; }

    /// <summary>
    /// Field values stored as JsonElement for AOT-safe serialization.
    /// </summary>
    public Dictionary<string, JsonElement> Fields { get; set; } = [];

        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
public DateTimeOffset? PublishedOn { get; set; }

    /// <summary>Monotonically incremented on each save. 0 = unsaved.</summary>
    public int VersionNumber { get; set; }

    /// <summary>If set, schedule this item for publishing at the given time.</summary>
    public DateTimeOffset? SchedulePublishUtc { get; set; }

    /// <summary>If set, schedule this item for unpublishing at the given time.</summary>
    public DateTimeOffset? ScheduleUnpublishUtc { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }
}
