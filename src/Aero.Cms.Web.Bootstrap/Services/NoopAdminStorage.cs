using Aero.Cms.Contracts.Abstractions;

namespace Aero.Cms.Web.Bootstrap.Services;

/// <summary>
/// Server-side storage shim used during prerendering, where browser localStorage is unavailable.
/// </summary>
internal sealed class NoopAdminStorage : IAdminStorage
{
        /// <summary>
    /// GetItem method.
    /// </summary>
public T? GetItem<T>(string key) => default;

        /// <summary>
    /// SetItem method.
    /// </summary>
public void SetItem<T>(string key, T value)
    {
    }
}
