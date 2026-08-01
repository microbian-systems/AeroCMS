using System.Collections.Concurrent;

namespace Aero.AppServer.Startup;

/// <summary>
/// Implements process-local, thread-safe, one-shot readiness signals.
/// </summary>
public sealed class MultiStartupSignal : IMultiStartupSignal
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> ready = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
public void MarkReady(string serviceName)
        => ready.GetOrAdd(serviceName, _ => CreateSignal()).TrySetResult(true);

    /// <inheritdoc />
public bool IsReady(string serviceName)
        => ready.TryGetValue(serviceName, out var signal) && signal.Task.IsCompletedSuccessfully;

    /// <inheritdoc />
public Task WaitForReadyAsync(string serviceName, CancellationToken cancellationToken = default)
        => ready.GetOrAdd(serviceName, _ => CreateSignal()).Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
public Task WaitForAllAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default)
        => Task.WhenAll(serviceNames.Select(name => WaitForReadyAsync(name, cancellationToken)));

    /// <summary>
    /// Creates a signal whose continuations do not run on the thread that publishes readiness.
    /// </summary>
    /// <returns>A new incomplete signal.</returns>
    private static TaskCompletionSource<bool> CreateSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
