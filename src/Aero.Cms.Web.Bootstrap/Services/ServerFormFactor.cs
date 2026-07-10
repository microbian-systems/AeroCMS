using Aero.Cms.Shared.Services;

namespace Aero.Cms.Web.Bootstrap.Services;

internal sealed class ServerFormFactor : IFormFactor
{
        /// <summary>
    /// GetFormFactor method.
    /// </summary>
public string GetFormFactor() => "Web";

        /// <summary>
    /// GetPlatform method.
    /// </summary>
public string GetPlatform() => Environment.OSVersion.ToString();
}
