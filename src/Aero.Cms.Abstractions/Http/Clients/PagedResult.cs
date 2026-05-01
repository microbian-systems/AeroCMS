namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>
/// Record for paged request parameters.
/// </summary>
/// <param name="Skip">Number of items to skip.</param>
/// <param name="Take">Number of items to take.</param>
/// <param name="Search">Optional search query.</param>
public record PagedRequest(int Skip = 0, int Take = 20, string? Search = null);

/// <summary>
/// Record for a paged result.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
/// <param name="Items">The list of items on current page.</param>
/// <param name="TotalCount">The total number of items available.</param>
/// <param name="Skip">The number of items skipped.</param>
/// <param name="Take">The number of items per page.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, long TotalCount, int Skip, int Take)
{
    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => Take > 0 ? (int)Math.Ceiling((double)TotalCount / Take) : 0;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => Skip + Take < TotalCount;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Skip > 0;
}
