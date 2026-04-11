namespace Aero.AppServer.Startup;

public interface IMultiStartupSignal
{
    void MarkReady(string serviceName);
    bool IsReady(string serviceName);
}
