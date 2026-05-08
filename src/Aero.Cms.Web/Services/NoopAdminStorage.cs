using Aero.Cms.Contracts.Abstractions;

namespace Aero.Cms.Web.Services;

/// <summary>
/// Server-side no-op <see cref="IAdminStorage"/> for use during prerendering.
/// During prerendering, browser localStorage doesn't exist, so all reads
/// return default and writes are silently discarded.
/// </summary>
internal sealed class NoopAdminStorage : IAdminStorage
{
    public T? GetItem<T>(string key) => default;
    public void SetItem<T>(string key, T value) { }
}
