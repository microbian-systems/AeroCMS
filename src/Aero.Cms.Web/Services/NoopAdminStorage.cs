using Aero.Cms.Contracts.Abstractions;

namespace Aero.Cms.Web.Services;

/// <summary>
/// Server-side no-op <see cref="IAdminStorage"/> for use during prerendering.
/// During prerendering, browser localStorage doesn't exist, so all reads
/// return default and writes are silently discarded.
/// </summary>
internal sealed class NoopAdminStorage : IAdminStorage
{
    /// <inheritdoc />
    /// <remarks>Always returns the default value because prerendering has no browser storage.</remarks>
public T? GetItem<T>(string key) => default;
    /// <inheritdoc />
    /// <remarks>Discards the value because prerendering has no browser storage.</remarks>
public void SetItem<T>(string key, T value) { }

    /// <inheritdoc />
    /// <remarks>Does nothing because prerendering has no browser storage.</remarks>
public void RemoveItem(string key) { }
}
