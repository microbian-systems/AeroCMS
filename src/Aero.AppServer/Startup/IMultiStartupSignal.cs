namespace Aero.AppServer.Startup;

public interface IMultiStartupSignal
{
    void MarkReady(string serviceName);
    bool IsReady(string serviceName);
    Task WaitForReadyAsync(string serviceName, CancellationToken cancellationToken = default);
    Task WaitForAllAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default);
}
