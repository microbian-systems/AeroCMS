#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
namespace Aero.Cms;

/// <summary>
/// Represents a class for Program.
/// </summary>
public class Program
{
        /// <summary>
    /// Main method.
    /// </summary>
public static void Main(string[] args)
    {
        // Dummy entry point for Aspire AppHost to satisfy build requirements for net10.0
        Console.WriteLine("Aero.Cms (MAUI) started as net10.0 dummy process.");
    }
}
#endif
