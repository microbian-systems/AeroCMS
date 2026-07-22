using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Recognizes setup, framework, static-asset, and health paths that remain reachable before setup completes.
/// </summary>
public sealed class SetupPathAllowlist
{
    /// <summary>
    /// Identifies the setup wizard route and redirect target.
    /// </summary>
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
        SetupPath,
        "/setup/",
        "/_framework",
        "/_content",
        "/_blazor",  // Blazor Server SignalR
        "/css",
        "/js",
        "/lib",
        "/assets",
        "/media",
        "/images",
        "/img",
        "/hydro"
    ];

    /// <summary>
    /// Determines whether a request path may bypass the setup gate.
    /// </summary>
    /// <param name="path">The request path to inspect.</param>
    /// <returns><see langword="true"/> for an exact or prefix match, ignoring case; otherwise <see langword="false"/>.</returns>
    /// <remarks>An empty path is rejected. Query strings are not part of <see cref="PathString"/> matching.</remarks>
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
