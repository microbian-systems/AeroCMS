using Android.App;
using Android.Runtime;

namespace Aero.Cms;

/// <summary>
/// Represents a class for MainApplication.
/// </summary>
[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
        /// <summary>
    /// CreateMauiApp method.
    /// </summary>
protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
