namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Service for initializing and persisting the state of modules during system setup.
/// </summary>
public interface IModuleInitializationService
{
    /// <summary>
    /// Discovers all available modules and persists their initial state to the database.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task InitializeModulesAsync(CancellationToken ct = default);
}
