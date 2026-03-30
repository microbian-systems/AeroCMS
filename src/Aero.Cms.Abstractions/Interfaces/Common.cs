namespace Aero.Cms.Abstractions.Interfaces;

public interface IHaveState<T> where T : class, new()
{
    Task<T> GetStateAsync(CancellationToken ct);
    Task UpdateStateAsync(T state, CancellationToken ct);
}

public interface ICanFindBySite<T, TKey>
    where T : class, new()
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
    Task<Result<AeroError, List<PageViewModel>>> GetBySiteIdAsync(
        TKey siteId,
        Expression<Func<T, bool>> predicaate,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default);
}

public interface ICanFindBySlug<T, TKey>
    where T : class, new()
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
    Task<Result<AeroError, T>> GetBySlugAsync(TKey siteId, string slug, CancellationToken ct = default);
}

public interface ICruddable<T, TKey>
    where T : class, new()
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
    Task<Result<AeroError, T>> GetByIdAsync(TKey id, CancellationToken ct = default);
    Task<Result<AeroError, T>> GetByIdsAsync(long[] ids, CancellationToken ct = default);
    Task<Result<AeroError, T>> CreateAsync(long siteId, T page, CancellationToken ct = default);
    Task<Result<AeroError, T>> UpdateAsync(T page, CancellationToken ct = default);
    Task<Result<AeroError, bool>> DeleteAsync(long id, CancellationToken ct = default);
}


public interface IContentGrain<T> :
    IGrainWithIntegerKey,
    ICruddable<T, long>,
    ICanFindBySite<T, long>,
    ICanFindBySlug<T, long>,
    ICanFindBySlug<T, string>,
    IHaveState<T>
    where T : class, new()
{
}

public interface IContentGrain<T, TKey> :
    IGrainWithIntegerKey,
    ICruddable<T, TKey>,
    ICanFindBySite<T, TKey>,
    ICanFindBySlug<T, string>,
    IHaveState<T>
    where T : class, new()
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
}
