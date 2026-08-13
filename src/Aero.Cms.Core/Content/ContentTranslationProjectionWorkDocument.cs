using AeroDB.Sable;

namespace Aero.Cms.Core.Content;

/// <summary>Durable, generation-fenced repair work for projections affected by group shared fields.</summary>
public sealed class ContentTranslationProjectionWorkDocument : SableDocument, IVersioned
{
    public long SiteId { get; set; }
    public long TranslationGroupId { get; set; }
    public long GroupStorageVersion { get; set; }
    public int GroupRevision { get; set; }
    public long? LastProcessedItemId { get; set; }
    public bool Completed { get; set; }
    public long Version { get; set; }
}
