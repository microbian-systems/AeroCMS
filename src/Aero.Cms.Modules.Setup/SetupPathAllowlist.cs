using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Setup;

public sealed class SetupPathAllowlist
{
    public const string SetupPath = "/setup";

    private static readonly string[] ExactPaths =
    [
        SetupPath,
        "/health",
        "/alive",
        "/error",
        "/not-found",
        "/favicon.ico",
        "/favicon.png",
        "/favicon-16x16.png",
        "/favicon-32x32.png",
        "/apple-touch-icon.png",
        "/site.webmanifest"
    ];

    private static readonly string[] PrefixPaths =
    [
        "/setup/",
        "/_framework",
        "/_content",
        "/css",
        "/js",
        "/images"
    ];

    public bool IsAllowed(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        var value = path.Value!;

        if (ExactPaths.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return PrefixPaths.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
