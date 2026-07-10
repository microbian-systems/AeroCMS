namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Defines an interface for IDatabaseBootstrapService.
/// </summary>
public interface IDatabaseBootstrapService
{
        /// <summary>
    /// PersistAsync method.
    /// </summary>
Task PersistAsync(DatabaseBootstrapModel model, CancellationToken cancellationToken = default);
}
