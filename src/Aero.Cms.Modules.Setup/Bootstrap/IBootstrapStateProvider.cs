namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Provides the effective bootstrap state used to choose setup or runtime behavior.
/// </summary>
public interface IBootstrapStateProvider
{
    /// <summary>
    /// Reads the current bootstrap state.
    /// </summary>
    /// <returns>A state snapshot derived from the provider's current configuration source.</returns>
BootstrapState GetState();
}
