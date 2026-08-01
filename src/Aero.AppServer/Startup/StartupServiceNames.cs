namespace Aero.AppServer.Startup;

/// <summary>
/// Defines stable names used to correlate local infrastructure readiness signals.
/// </summary>
public static class StartupServiceNames
{
        /// <summary>
        /// Gets the embedded AeroDB readiness-signal name.
        /// </summary>
        public const string AeroDb = nameof(AeroDb);
        /// <summary>
    /// Gets the local Garnet readiness-signal name.
    /// </summary>
public const string Garnet = nameof(Garnet);
}
