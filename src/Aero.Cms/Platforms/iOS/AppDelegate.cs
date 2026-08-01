using Foundation;

namespace Aero.Cms;

/// <summary>
/// Bridges iOS application startup to the shared MAUI host factory.
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    /// <inheritdoc />
protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
