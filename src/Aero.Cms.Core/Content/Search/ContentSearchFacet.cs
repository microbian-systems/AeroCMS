using Aero.Cms.Abstractions.Enums;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Search;

/// <summary>Normalized exact-match value emitted for an indexed runtime content field.</summary>
public sealed class ContentSearchFacet : SableDocument
{
    public long SiteId { get; set; }
    public long ContentItemId { get; set; }
    public string ContentTypeAlias { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
    public ContentPublicationState PublicationState { get; set; }
    public bool HideFromSearch { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public int Ordinal { get; set; }
}
