using Aero.Cms.Shared.Services;

namespace Aero.Cms.Web.Bootstrap.Services;

internal sealed class ServerFormFactor : IFormFactor
{
    /// <summary>
    /// Identifies the server host as the web form factor.
    /// </summary>
    /// <returns>The fixed value <c>Web</c>.</returns>
public string GetFormFactor() => "Web";

    /// <summary>
    /// Describes the operating system on which the server process is running.
    /// </summary>
    /// <returns>The current <see cref="Environment.OSVersion"/> formatted as a string.</returns>
public string GetPlatform() => Environment.OSVersion.ToString();
}
