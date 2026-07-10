using Aero.Cms.Shared.Services;

namespace Aero.Cms.Web.Services;

/// <summary>
/// Represents a class for FormFactor.
/// </summary>
public class FormFactor : IFormFactor
{
        /// <summary>
    /// GetFormFactor method.
    /// </summary>
public string GetFormFactor()
    {
        return "Web";
    }

        /// <summary>
    /// GetPlatform method.
    /// </summary>
public string GetPlatform()
    {
        return Environment.OSVersion.ToString();
    }
}
