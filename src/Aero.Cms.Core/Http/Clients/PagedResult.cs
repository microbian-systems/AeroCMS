namespace Aero.Cms.Core.Http.Clients;

public record PagedRequest(int Skip = 0, int Take = 20, string? Search = null);

public record PagedResult<T>(IReadOnlyList<T> Items, long TotalCount, int Skip, int Take)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Take);
    public bool HasNextPage => Skip + Take < TotalCount;
    public bool HasPreviousPage => Skip > 0;
}
