namespace Aero.AppServer.Startup;

/// <summary>
/// Coordinates one-shot readiness signals for named infrastructure services.
/// </summary>
public interface IMultiStartupSignal
{
    /// <summary>
    /// Marks a service ready and releases all current and future waiters.
    /// </summary>
    /// <param name="serviceName">The case-insensitive service name.</param>
void MarkReady(string serviceName);
    /// <summary>
    /// Determines whether a named service has completed its readiness signal successfully.
    /// </summary>
    /// <param name="serviceName">The case-insensitive service name.</param>
    /// <returns><see langword="true"/> when readiness has been published; otherwise, <see langword="false"/>.</returns>
bool IsReady(string serviceName);
    /// <summary>
    /// Waits until a named service publishes readiness.
    /// </summary>
    /// <param name="serviceName">The case-insensitive service name.</param>
    /// <param name="cancellationToken">Cancels only this wait, not the shared readiness signal.</param>
    /// <returns>A task that completes when the service is ready.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled first.
    /// </exception>
Task WaitForReadyAsync(string serviceName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Waits for every supplied service name to publish readiness.
    /// </summary>
    /// <param name="serviceNames">The service names to await; duplicate names share one signal.</param>
    /// <param name="cancellationToken">Cancels the outstanding waits.</param>
    /// <returns>A task that completes after all named services are ready.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled first.
    /// </exception>
Task WaitForAllAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default);
}
