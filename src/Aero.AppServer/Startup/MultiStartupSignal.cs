using System.Collections.Concurrent;

namespace Aero.AppServer.Startup;

/// <summary>
/// Represents a class for MultiStartupSignal.
/// </summary>
public sealed class MultiStartupSignal : IMultiStartupSignal
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> ready = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
    /// MarkReady method.
    /// </summary>
public void MarkReady(string serviceName)
        => ready.GetOrAdd(serviceName, _ => CreateSignal()).TrySetResult(true);

        /// <summary>
    /// IsReady method.
    /// </summary>
public bool IsReady(string serviceName)
        => ready.TryGetValue(serviceName, out var signal) && signal.Task.IsCompletedSuccessfully;

        /// <summary>
    /// WaitForReadyAsync method.
    /// </summary>
public Task WaitForReadyAsync(string serviceName, CancellationToken cancellationToken = default)
        => ready.GetOrAdd(serviceName, _ => CreateSignal()).Task.WaitAsync(cancellationToken);

        /// <summary>
    /// WaitForAllAsync method.
    /// </summary>
public Task WaitForAllAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default)
        => Task.WhenAll(serviceNames.Select(name => WaitForReadyAsync(name, cancellationToken)));

    private static TaskCompletionSource<bool> CreateSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
