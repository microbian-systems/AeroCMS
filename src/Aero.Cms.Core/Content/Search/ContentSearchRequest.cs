namespace Aero.Cms.Core.Content.Search;

public enum ContentSearchMode
{
    FullText = 0,
    Semantic = 1
}

public sealed record ContentSearchRequest(
    long SiteId,
    string ContentTypeAlias,
    string Query,
    string? Culture,
    ContentSearchMode Mode,
    bool PublishedOnly,
    int Skip,
    int Take,
    IReadOnlyDictionary<string, string> ExactFilters);

public sealed record ContentSearchResult(
    IReadOnlyList<Aero.Cms.Abstractions.Content.ContentItem> Items,
    bool HasMore);
