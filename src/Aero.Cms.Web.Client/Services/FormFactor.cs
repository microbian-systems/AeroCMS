using Aero.Cms.Shared.Services;

namespace Aero.Cms.Web.Client.Services;

/// <summary>
/// Reports browser-host information to shared form-factor-aware components.
/// </summary>
public class FormFactor : IFormFactor
{
    /// <summary>
    /// Identifies the form factor as Blazor WebAssembly.
    /// </summary>
    /// <returns>The constant <c>WebAssembly</c>.</returns>
public string GetFormFactor()
    {
        return "WebAssembly";
    }

    /// <summary>
    /// Returns the operating-system description exposed by the WebAssembly runtime.
    /// </summary>
    /// <returns><see cref="Environment.OSVersion"/> formatted as text.</returns>
public string GetPlatform()
    {
        return Environment.OSVersion.ToString();
    }
}
