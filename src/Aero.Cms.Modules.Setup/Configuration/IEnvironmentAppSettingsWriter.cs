namespace Aero.Cms.Modules.Setup.Configuration;

public interface IEnvironmentAppSettingsWriter
{
    Task WriteAsync(string environmentName, string json, CancellationToken cancellationToken = default);
}
