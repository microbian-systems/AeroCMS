namespace Aero.Cms.Contracts.Abstractions;

/// <summary>
/// Abstracts optional key-based state used by manager components.
/// </summary>
/// <remarks>
/// This contract does not guarantee persistence, durability, or read-after-write behavior.
/// Browser hosts may provide persistent storage, while server-side prerendering may use a
/// no-op implementation that returns default values and discards writes. Implementations
/// also define their own serialization and exception behavior.
/// </remarks>
public interface IAdminStorage
{
    /// <summary>
    /// Requests the value associated with a key from the configured implementation.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The storage key to look up.</param>
    /// <returns>The implementation-provided value, which may be the default value of <typeparamref name="T"/>.</returns>
    /// <remarks>Missing-key and conversion-failure behavior is implementation-specific.</remarks>
    T? GetItem<T>(string key);

    /// <summary>
    /// Offers a key and value to the configured storage implementation.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The storage key to update.</param>
    /// <param name="value">The value to store.</param>
    /// <remarks>Normal completion does not guarantee that the value was retained.</remarks>
    void SetItem<T>(string key, T value);

    /// <summary>
    /// Requests removal of the value associated with a key.
    /// </summary>
    /// <param name="key">The storage key to remove.</param>
    /// <remarks>Normal completion does not guarantee that the value was removed.</remarks>
    void RemoveItem(string key);
}
