using Aero.Cms.Abstractions.Enums;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Search;

/// <summary>Persisted, provider-backed full-text projection for one content item.</summary>
public sealed class ContentSearchDocument : SableDocument
{
    public long SiteId { get; set; }
    public long ContentItemId { get; set; }
    public string ContentTypeAlias { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
    public ContentPublicationState PublicationState { get; set; }
    public DateTimeOffset? PublishedOn { get; set; }
    public int VersionNumber { get; set; }
    public int SearchSchemaVersion { get; set; } = ContentSearchConstants.SchemaVersion;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool HideFromSearch { get; set; }
    public string FullText { get; set; } = string.Empty;
}
