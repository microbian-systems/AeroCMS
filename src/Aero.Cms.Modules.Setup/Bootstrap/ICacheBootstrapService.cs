namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Defines an interface for ICacheBootstrapService.
/// </summary>
public interface ICacheBootstrapService
{
        /// <summary>
    /// PersistAsync method.
    /// </summary>
Task PersistAsync(CacheBootstrapModel model, CancellationToken cancellationToken = default);
}
