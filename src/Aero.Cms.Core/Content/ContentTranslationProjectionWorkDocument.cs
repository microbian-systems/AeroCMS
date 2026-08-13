using AeroDB.Sable;

namespace Aero.Cms.Core.Content;

/// <summary>Durable, generation-fenced repair work for projections affected by group shared fields.</summary>
public sealed class ContentTranslationProjectionWorkDocument : SableDocument, IVersioned
{
    /// <summary>Stable identity of the group generation this work repairs.</summary>
    public string WorkKey { get; set; } = string.Empty;
    public long SiteId { get; set; }
    public long TranslationGroupId { get; set; }
    public long GroupStorageVersion { get; set; }
    public int GroupRevision { get; set; }
    public long? LastProcessedItemId { get; set; }
    public bool Completed { get; set; }
    /// <summary>Number of durable processing attempts, including cache retry attempts.</summary>
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptOn { get; set; }
    public string? LastFailure { get; set; }
    public long Version { get; set; }
}
