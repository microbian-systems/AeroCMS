using Aero.Cms.Abstractions.Enums;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Search;

/// <summary>Model-versioned vector projection for one content item.</summary>
public sealed class ContentSemanticDocument : SableDocument
{
    public long SiteId { get; set; }
    public long ContentItemId { get; set; }
    public string ContentTypeAlias { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
    public ContentPublicationState PublicationState { get; set; }
    public DateTimeOffset? PublishedOn { get; set; }
    public bool HideFromSearch { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; }
    public float[] Embedding { get; set; } = [];
}
