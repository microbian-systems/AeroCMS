namespace Aero.AppServer.Startup;

/// <summary>
/// Defines an interface for IMultiStartupSignal.
/// </summary>
public interface IMultiStartupSignal
{
        /// <summary>
    /// MarkReady method.
    /// </summary>
void MarkReady(string serviceName);
        /// <summary>
    /// IsReady method.
    /// </summary>
bool IsReady(string serviceName);
        /// <summary>
    /// WaitForReadyAsync method.
    /// </summary>
Task WaitForReadyAsync(string serviceName, CancellationToken cancellationToken = default);
        /// <summary>
    /// WaitForAllAsync method.
    /// </summary>
Task WaitForAllAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default);
}
