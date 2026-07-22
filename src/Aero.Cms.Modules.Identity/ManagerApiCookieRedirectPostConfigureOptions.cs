using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Prevents cookie handlers from redirecting API clients to HTML login pages.
/// </summary>
internal sealed class ManagerApiCookieRedirectPostConfigureOptions
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var redirectToLogin = options.Events.OnRedirectToLogin;
        var redirectToAccessDenied = options.Events.OnRedirectToAccessDenied;

        options.Events.OnRedirectToLogin = context =>
            IsApiRequest(context.Request)
                ? SetStatusCodeAsync(context.Response, StatusCodes.Status401Unauthorized)
                : redirectToLogin(context);

        options.Events.OnRedirectToAccessDenied = context =>
            IsApiRequest(context.Request)
                ? SetStatusCodeAsync(context.Response, StatusCodes.Status403Forbidden)
                : redirectToAccessDenied(context);
    }

    private static bool IsApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    private static Task SetStatusCodeAsync(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        response.Headers.Remove("Location");
        return Task.CompletedTask;
    }
}
