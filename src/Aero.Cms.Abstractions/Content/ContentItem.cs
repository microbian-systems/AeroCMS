using System.Text.Json;
using Aero.Cms.Abstractions.Enums;
using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Content;

public sealed class ContentItem : Entity
{
    public long SiteId { get; set; }
    public string ContentTypeAlias { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Title { get; set; }

    /// <summary>
    /// Field values stored as JsonElement for AOT-safe serialization.
    /// </summary>
    public Dictionary<string, JsonElement> Fields { get; set; } = [];

    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; }

    /// <summary>Monotonically incremented on each save. 0 = unsaved.</summary>
    public int VersionNumber { get; set; }

    /// <summary>If set, schedule this item for publishing at the given time.</summary>
    public DateTimeOffset? SchedulePublishUtc { get; set; }

    /// <summary>If set, schedule this item for unpublishing at the given time.</summary>
    public DateTimeOffset? ScheduleUnpublishUtc { get; set; }
}
