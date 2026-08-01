using Aero.Cms.Shared.Services;

namespace Aero.Cms.Services;

/// <summary>
/// Reports MAUI device idiom and platform information to shared components.
/// </summary>
public class FormFactor : IFormFactor
{
    /// <summary>
    /// Returns the current MAUI device idiom.
    /// </summary>
    /// <returns>The <see cref="DeviceInfo.Idiom"/> string representation.</returns>
public string GetFormFactor()
    {
        return DeviceInfo.Idiom.ToString();
    }

    /// <summary>
    /// Returns the current MAUI platform and operating-system version.
    /// </summary>
    /// <returns>A platform name and version separated by <c> - </c>.</returns>
public string GetPlatform()
    {
        return DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
    }
}
