using Aero.Cms.Contracts.Abstractions;

namespace Aero.Cms.Web.Bootstrap.Services;

/// <summary>
/// Server-side storage shim used during prerendering, where browser localStorage is unavailable.
/// </summary>
internal sealed class NoopAdminStorage : IAdminStorage
{
    /// <inheritdoc />
    /// <remarks>This server-side implementation does not read persistent state and always returns the default value.</remarks>
public T? GetItem<T>(string key) => default;

    /// <inheritdoc />
    /// <remarks>This server-side implementation discards the key and value without persisting them.</remarks>
public void SetItem<T>(string key, T value)
    {
    }

    /// <inheritdoc />
    /// <remarks>This server-side implementation has no browser storage to modify.</remarks>
public void RemoveItem(string key)
    {
    }
}
