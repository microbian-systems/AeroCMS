namespace Aero.Cms.Abstractions.Content.Composition;

/// <summary>Read-only published content data exposed to page composition.</summary>
public sealed record PublishedContentItemProjection
{
    /// <summary>Gets the stable content-item identifier.</summary>
    public long Id { get; init; }

    /// <summary>Gets the current content-type alias.</summary>
    public string ContentTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the item routing slug.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Gets the item culture.</summary>
    public string Culture { get; init; } = string.Empty;

    /// <summary>Gets an independent field-value snapshot.</summary>
    public IReadOnlyDictionary<string, JsonElement> Fields { get; init; }
        = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One bounded page of published content projections.</summary>
public sealed record PublishedContentPage
{
    /// <summary>Gets the authoritative content-type alias used for this query.</summary>
    public string ContentTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the projected items on this page.</summary>
    public IReadOnlyList<PublishedContentItemProjection> Items { get; init; } = [];

    /// <summary>Gets the total number of items after publication, culture, and filter checks.</summary>
    public long TotalCount { get; init; }

    /// <summary>Gets whether <see cref="TotalCount"/> is the complete result count.</summary>
    public bool IsTotalCountExact { get; init; } = true;

    /// <summary>Gets whether a subsequent page may contain additional entries.</summary>
    public bool HasMore { get; init; }

    /// <summary>Gets the one-based requested page number.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Gets the configured page size.</summary>
    public int PageSize { get; init; }
}
