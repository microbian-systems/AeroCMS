using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Represents a class for ContentItem.
/// </summary>
public sealed class ContentItem : SableDocument, IAuditable, IVersioned
{
    private ContentTranslationReview translationReview = new();
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
    /// Gets or sets immutable-at-creation information about how this localized variant was made.
    /// The source item, when applicable, remains represented by <see cref="SourceItemId"/>.
    /// </summary>
    public ContentTranslationProvenance? TranslationProvenance { get; set; }

    /// <summary>
    /// Gets or sets the current review state for a translation generated or assisted by AI.
    /// Manual and copied variants do not require review unless an implementing policy says otherwise.
    /// </summary>
    public ContentTranslationReview TranslationReview
    {
        get => translationReview;
        set => translationReview = value ?? new();
    }

    /// <summary>
    /// Gets or sets the source-of-truth parent item identifier for hierarchical content.
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>Gets or sets the stable order among siblings.</summary>
    public int SortOrder { get; set; }

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

    /// <summary>Storage concurrency token; distinct from the editorial version number.</summary>
    public long Version { get; set; }

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
