namespace Aero.Cms.Core.Content.Search;

/// <summary>
/// Represents a content item in the search index, containing extracted field tokens
/// for full-text and faceted search.
/// </summary>
public sealed class ContentSearchDocument
{
    /// <summary>Document ID in the format "content:{siteId}:{contentItemId}".</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The site this content item belongs to.</summary>
    public long SiteId { get; set; }

    /// <summary>The content item ID.</summary>
    public long ContentItemId { get; set; }

    /// <summary>The content type alias.</summary>
    public string ContentTypeAlias { get; set; } = string.Empty;

    /// <summary>The item slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether this document should be excluded from site-wide search results.</summary>
    public bool HideFromSearch { get; set; }

    /// <summary>Concatenated tokens from all indexed fields, for full-text search.</summary>
    public string FullText { get; set; } = string.Empty;

    /// <summary>Per-field tokens for faceted search.</summary>
    public Dictionary<string, List<string>> FieldTokens { get; set; } = [];
}
