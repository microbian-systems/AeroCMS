#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
namespace Aero.Cms;

/// <summary>
/// Provides the non-mobile placeholder entry point used by Aspire builds.
/// </summary>
public class Program
{
    /// <summary>
    /// Writes a startup marker and exits without creating the MAUI UI.
    /// </summary>
    /// <param name="args">Process arguments; the placeholder does not inspect them.</param>
public static void Main(string[] args)
    {
        // Dummy entry point for Aspire AppHost to satisfy build requirements for net10.0
        Console.WriteLine("Aero.Cms (MAUI) started as net10.0 dummy process.");
    }
}
#endif
