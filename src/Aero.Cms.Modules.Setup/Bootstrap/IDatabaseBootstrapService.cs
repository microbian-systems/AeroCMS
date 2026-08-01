namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Persists the database and credential settings required before the runtime host can start.
/// </summary>
public interface IDatabaseBootstrapService
{
    /// <summary>
    /// Protects and persists database bootstrap settings.
    /// </summary>
    /// <param name="model">Database mode, authentication, and secret-provider inputs.</param>
    /// <param name="cancellationToken">Cancels settings file I/O.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
Task PersistAsync(DatabaseBootstrapModel model, CancellationToken cancellationToken = default);
}
