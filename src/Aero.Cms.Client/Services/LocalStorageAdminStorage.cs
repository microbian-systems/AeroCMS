using Aero.Cms.Contracts.Abstractions;
using Microsoft.JSInterop;

namespace Aero.Cms.Web.Client.Services;

/// <summary>
/// Implements synchronous administrative state storage over browser local storage.
/// </summary>
/// <param name="localStorage">The WebAssembly local-storage service.</param>
/// <remarks>
/// These synchronous calls require the in-process WebAssembly JavaScript runtime and browser
/// storage availability. Serialization and JavaScript interop exceptions are propagated.
/// </remarks>
public sealed class LocalStorageAdminStorage(ILocalStorageService localStorage) : IAdminStorage
{
    /// <summary>
    /// Deserializes a value from browser local storage.
    /// </summary>
    /// <typeparam name="T">The stored value type.</typeparam>
    /// <param name="key">The local-storage key.</param>
    /// <returns>The stored value, or the service's default value when the key is absent.</returns>
public T? GetItem<T>(string key) => localStorage.GetItem<T>(key);
    /// <summary>
    /// Serializes and stores a value in browser local storage.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The local-storage key.</param>
    /// <param name="value">The value to persist.</param>
public void SetItem<T>(string key, T value) => localStorage.SetItem(key, value);

    /// <summary>
    /// Removes a value from browser local storage.
    /// </summary>
    /// <param name="key">The key to remove.</param>
public void RemoveItem(string key) => localStorage.RemoveItem(key);
}
