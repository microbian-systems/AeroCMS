using UIKit;

namespace Aero.Cms;

/// <summary>
/// Contains the native iOS application entry point.
/// </summary>
public class Program
{
    // This is the main entry point of the application.
    /// <summary>
    /// Starts UIKit with <see cref="AppDelegate"/> as the application delegate.
    /// </summary>
    /// <param name="args">Native process arguments forwarded to UIKit.</param>
    static void Main(string[] args)
    {
        // if you want to use a different Application Delegate class from "AppDelegate"
        // you can specify it here.
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
