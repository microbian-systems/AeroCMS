using Aero.Cms.Shared.Services;
using Microsoft.JSInterop;

namespace Aero.Cms.Web.Client.Services;

/// <summary>
/// WASM implementation of <see cref="IAdminStorage"/> backed by
/// the Blazor.LocalStorage.WebAssembly <see cref="ILocalStorageService"/>.
/// </summary>
public sealed class LocalStorageAdminStorage(ILocalStorageService localStorage) : IAdminStorage
{
    public T? GetItem<T>(string key) => localStorage.GetItem<T>(key);
    public void SetItem<T>(string key, T value) => localStorage.SetItem(key, value);
}
