using Android.App;
using Android.Runtime;

namespace Aero.Cms;

/// <summary>
/// Bridges Android application startup to the shared MAUI host factory.
/// </summary>
/// <param name="handle">The native application handle supplied by Android.</param>
/// <param name="ownership">The JNI handle ownership mode.</param>
[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    /// <inheritdoc />
protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
