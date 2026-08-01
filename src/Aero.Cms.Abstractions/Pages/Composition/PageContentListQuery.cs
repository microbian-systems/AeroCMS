namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Defines the bounded paging, sorting, and filtering applied to a content list.
/// </summary>
public sealed record PageContentListQuery
{
    /// <summary>Gets the smallest supported public page size.</summary>
    public const int MinimumPageSize = 1;

    /// <summary>Gets the largest supported public page size.</summary>
    public const int MaximumPageSize = 100;

    /// <summary>Gets the largest supported number of AND filters.</summary>
    public const int MaximumFilterCount = 10;

    /// <summary>Gets the number of entries requested per public page.</summary>
    public int PageSize { get; init; } = 10;

    /// <summary>Gets the optional content-field name used for sorting.</summary>
    public string? SortField { get; init; }

    /// <summary>Gets the requested sort direction.</summary>
    public PageContentSortDirection SortDirection { get; init; } =
        PageContentSortDirection.Ascending;

    /// <summary>Gets the filters joined by logical AND.</summary>
    public IReadOnlyList<PageContentFilter> Filters { get; init; } = [];

    /// <summary>Creates an independent query copy.</summary>
    /// <returns>A query with an independent filter collection.</returns>
    public PageContentListQuery CreateSnapshot() => this with { Filters = (Filters ?? []).ToArray() };
}
