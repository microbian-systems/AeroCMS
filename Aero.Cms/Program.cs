#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
namespace Aero.Cms;

public class Program
{
    public static void Main(string[] args)
    {
        // Dummy entry point for Aspire AppHost to satisfy build requirements for net10.0
        Console.WriteLine("Aero.Cms (MAUI) started as net10.0 dummy process.");
    }
}
#endif
