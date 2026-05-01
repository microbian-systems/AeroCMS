using System.Collections.Concurrent;
using System;

namespace Aero.AppServer.Startup;

public sealed class MultiStartupSignal : IMultiStartupSignal
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> ready = new(StringComparer.OrdinalIgnoreCase);

    public void MarkReady(string serviceName)
        => ready.GetOrAdd(serviceName, _ => CreateSignal()).TrySetResult(true);

    public bool IsReady(string serviceName)
        => ready.TryGetValue(serviceName, out var signal) && signal.Task.IsCompletedSuccessfully;

    public Task WaitForReadyAsync(string serviceName, CancellationToken cancellationToken = default)
        => ready.GetOrAdd(serviceName, _ => CreateSignal()).Task.WaitAsync(cancellationToken);

    public Task WaitForAllAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default)
        => Task.WhenAll(serviceNames.Select(name => WaitForReadyAsync(name, cancellationToken)));

    private static TaskCompletionSource<bool> CreateSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
