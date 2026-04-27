

namespace Aero.Cms.Abstractions.Interfaces;

public interface IHaveState<T> where T : AeroEntityViewModel
{
    Task<T> GetStateAsync(CancellationToken ct);
    Task UpdateStateAsync(T state, CancellationToken ct);
}

public interface ICanSearch
{
    Task SearchAsync(AeroSearchFilter filter, int page = 1, int rows = 10, CancellationToken ct = default);
}

public interface ICanFindBySite<T, TKey>
    where T : AeroEntityViewModel
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
    Task<AeroRequestResponse<T>> GetBySiteIdAsync(
        TKey siteId,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default);
}

public interface ICanFindBySlug<T, TKey>
    where T : AeroEntityViewModel
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
    Task<AeroRequestResponse<T>> GetBySlugAsync(TKey siteId, string slug, CancellationToken ct = default);
}

public interface ICruddable<T, TKey>
    where T : AeroEntityViewModel
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
    Task<AeroRequestResponse<T>> GetByIdAsync(TKey id, CancellationToken ct = default);
    Task<AeroRequestResponse<T>> GetByIdsAsync(TKey[] ids, CancellationToken ct = default);
    Task<AeroRequestResponse<T>> CreateAsync(IRequest request, CancellationToken ct = default);
    Task<AeroRequestResponse<T>> UpdateAsync(IRequest request, CancellationToken ct = default);
    Task<AeroRequestResponse<T>> DeleteAsync(IRequest request, CancellationToken ct = default);
}


