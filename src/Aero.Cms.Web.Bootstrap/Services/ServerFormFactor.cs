using Aero.Cms.Shared.Services;

namespace Aero.Cms.Web.Bootstrap.Services;

internal sealed class ServerFormFactor : IFormFactor
{
    public string GetFormFactor() => "Web";

    public string GetPlatform() => Environment.OSVersion.ToString();
}
