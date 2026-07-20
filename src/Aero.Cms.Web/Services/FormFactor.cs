using Aero.Cms.Shared.Services;

namespace Aero.Cms.Web.Services;

/// <summary>
/// Describes the server-rendered web host's form factor and operating-system platform.
/// </summary>
public class FormFactor : IFormFactor
{
    /// <inheritdoc />
public string GetFormFactor()
    {
        return "Web";
    }

    /// <inheritdoc />
public string GetPlatform()
    {
        return Environment.OSVersion.ToString();
    }
}
