using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Rendering;
using Aero.Cms.Shared.Localization;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Content;

/// <summary>
/// Registers the public content type URL route as a low-priority fallback
/// so dev-time Razor Pages and CMS page routes take precedence.
/// </summary>
public static class ContentTypeRouteHandler
{
    private static readonly HashSet<string> ReservedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "manager", "api", "account", "login", "logout",
        "register", "health", "swagger", "scalar", "favicon.ico"
    };

    /// <summary>
    /// Maps public content type routes at /{typeAlias}/{entrySlug}.
    /// Registered with the lowest possible priority so explicit Razor Pages
    /// and page catch-all routes always match first.
    /// </summary>
    public static void MapContentTypeRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{typeAlias}/{entrySlug}", HandleContentTypeRequest)
            .WithName("GetContentItemBySlug")
            .WithTags("Content");
    }

    private static async Task<IResult> HandleContentTypeRequest(
        string typeAlias,
        string entrySlug,
        HttpContext context,
        ISiteContext siteContext,
        ContentTypeUrlRenderer renderer,
        CancellationToken ct)
    {
        var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("ContentTypeRoute");

        logger.LogWarning("ContentTypeRoute HIT: typeAlias={TypeAlias}, entrySlug={EntrySlug}", typeAlias, entrySlug);

        // Fast reject: reserved prefixes or admin paths
        if (IsReservedPrefix(typeAlias))
        {
            logger.LogWarning("ContentTypeRoute REJECTED: reserved prefix '{TypeAlias}'", typeAlias);
            return TypedResults.NotFound();
        }

        // Strip any leading culture prefix from the type alias
        var normalizedType = AeroCultureRoute.StripLeadingCulture(typeAlias);
        logger.LogWarning("ContentTypeRoute NORMALIZED: type={NormalizedType}", normalizedType);

        if (string.IsNullOrWhiteSpace(normalizedType))
            return TypedResults.NotFound();

        var normalizedSlug = (entrySlug ?? string.Empty).Trim().TrimEnd('/').TrimStart('/').TrimEnd('.');
        if (string.IsNullOrWhiteSpace(normalizedSlug))
            return TypedResults.NotFound();

        var siteId = siteContext.SiteId;
        logger.LogWarning("ContentTypeRoute SITECTX: siteId={SiteId}, attempting lookup type={Type}, slug={Slug}",
            siteId, normalizedType, normalizedSlug);

        var result = await renderer.RenderAsync(siteId, normalizedType, normalizedSlug, ct);
        if (result is Result<string, AeroError>.Ok ok)
        {
            logger.LogWarning("ContentTypeRoute SUCCESS: htmlLen={Len}, preview={Preview}",
                ok.Value.Length, ok.Value.Length > 200 ? ok.Value[..200] : ok.Value);
            return TypedResults.Content(ok.Value, "text/html");
        }

        var reason = result is Result<string, AeroError>.Failure f
            ? FormatError(f.Error)
            : "Unknown failure";
        logger.LogWarning("ContentTypeRoute 404: {Type}/{Slug} — reason: {Reason}",
            normalizedType, normalizedSlug, reason);
        return TypedResults.NotFound();
    }

    private static bool IsReservedPrefix(string segment)
        => ReservedPrefixes.Contains(segment);

    private static string FormatError(AeroError error)
    {
        if (error is AeroError.Validation v)
        {
            var messages = v.Errors.Count > 0
                ? string.Join(" | ", v.Errors)
                : "(no details)";
            return $"Validation [{messages}]";
        }

        return error.ToString();
    }
}
