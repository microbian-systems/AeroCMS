using System.Collections.Concurrent;
using System;

namespace Aero.AppServer.Startup;

public sealed class MultiStartupSignal : IMultiStartupSignal
{
    private readonly ConcurrentDictionary<string, bool> ready = new(StringComparer.OrdinalIgnoreCase);

    public void MarkReady(string serviceName) => ready[serviceName] = true;

    public bool IsReady(string serviceName) => ready.ContainsKey(serviceName);
}
