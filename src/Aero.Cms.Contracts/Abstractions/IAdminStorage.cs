namespace Aero.Cms.Contracts.Abstractions;

/// <summary>
/// Simple abstraction over browser localStorage for testability and DI.
/// The WASM client registers a <see cref="Microsoft.JSInterop.ILocalStorageService"/>-backed implementation.
/// </summary>
public interface IAdminStorage
{
    T? GetItem<T>(string key);
    void SetItem<T>(string key, T value);
}
