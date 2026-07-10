

namespace Aero.Cms.Abstractions.Interfaces;

/// <summary>
/// Defines an interface for IHaveState.
/// </summary>
public interface IHaveState<T> where T : AeroEntityViewModel
{
        /// <summary>
    /// GetStateAsync method.
    /// </summary>
Task<T> GetStateAsync(CancellationToken ct);
        /// <summary>
    /// UpdateStateAsync method.
    /// </summary>
Task UpdateStateAsync(T state, CancellationToken ct);
}

/// <summary>
/// Defines an interface for ICanSearch.
/// </summary>
public interface ICanSearch
{
        /// <summary>
    /// SearchAsync method.
    /// </summary>
Task SearchAsync(AeroSearchFilter filter, int page = 1, int rows = 10, CancellationToken ct = default);
}

/// <summary>
/// Defines an interface for ICanFindBySite.
/// </summary>
public interface ICanFindBySite<T, TKey>
    where T : AeroEntityViewModel
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
Task<AeroRequestResponse<T>> GetBySiteIdAsync(
        TKey siteId,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default);
}

/// <summary>
/// Defines an interface for ICanFindBySlug.
/// </summary>
public interface ICanFindBySlug<T, TKey>
    where T : AeroEntityViewModel
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
Task<AeroRequestResponse<T>> GetBySlugAsync(TKey siteId, string slug, CancellationToken ct = default);
}

/// <summary>
/// Defines an interface for ICruddable.
/// </summary>
public interface ICruddable<T, TKey>
    where T : AeroEntityViewModel
    where TKey : IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<AeroRequestResponse<T>> GetByIdAsync(TKey id, CancellationToken ct = default);
        /// <summary>
    /// GetByIdsAsync method.
    /// </summary>
Task<AeroRequestResponse<T>> GetByIdsAsync(TKey[] ids, CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<AeroRequestResponse<T>> CreateAsync(IRequest request, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<AeroRequestResponse<T>> UpdateAsync(IRequest request, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<AeroRequestResponse<T>> DeleteAsync(IRequest request, CancellationToken ct = default);
}


