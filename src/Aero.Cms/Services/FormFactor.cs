using Aero.Cms.Shared.Services;

namespace Aero.Cms.Services;

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
        return DeviceInfo.Idiom.ToString();
    }

        /// <summary>
    /// GetPlatform method.
    /// </summary>
public string GetPlatform()
    {
        return DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
    }
}
