namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Persists the cache settings required before the runtime host can start.
/// </summary>
public interface ICacheBootstrapService
{
    /// <summary>
    /// Validates, protects, and persists cache bootstrap settings.
    /// </summary>
    /// <param name="model">Cache mode, connection, and secret-provider inputs.</param>
    /// <param name="cancellationToken">Cancels settings file I/O.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The cache mode is neither local nor server.</exception>
    /// <exception cref="ArgumentException">Server mode does not include a connection string.</exception>
Task PersistAsync(CacheBootstrapModel model, CancellationToken cancellationToken = default);
}
