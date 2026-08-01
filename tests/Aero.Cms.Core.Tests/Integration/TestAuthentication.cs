using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Core.Tests.Integration;

internal static class TestAuthentication
{
    public const string Scheme = "AeroCms.Tests";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string RoleHeader = "X-Test-Role";
    public const string IsAdminHeader = "X-Test-Is-Admin";

    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = Scheme;
                options.DefaultChallengeScheme = Scheme;
                options.DefaultForbidScheme = Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(Scheme, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AeroAdmin", policy => policy.RequireRole("Admin"));
            options.AddPolicy("site:read", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("site:create", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("site:update", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("site:delete", policy => policy.RequireAuthenticatedUser());
        });

        return services;
    }

    public static HttpRequestMessage WithTestUser(
        this HttpRequestMessage request,
        long userId,
        string? role = null,
        bool isAdmin = false)
    {
        request.Headers.Add(UserIdHeader, userId.ToString());
        if (!string.IsNullOrWhiteSpace(role))
            request.Headers.Add(RoleHeader, role);
        if (isAdmin)
            request.Headers.Add(IsAdminHeader, "true");
        return request;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserIdHeader, out var userId)
                || string.IsNullOrWhiteSpace(userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new("sub", userId.ToString())
            };

            if (Request.Headers.TryGetValue(RoleHeader, out var role)
                && !string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }

            if (Request.Headers.TryGetValue(IsAdminHeader, out var isAdmin)
                && string.Equals(isAdmin, "true", StringComparison.Ordinal))
            {
                claims.Add(new Claim("is_admin", "true"));
            }

            var identity = new ClaimsIdentity(
                claims,
                TestAuthentication.Scheme,
                ClaimTypes.Name,
                ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, TestAuthentication.Scheme)));
        }
    }
}
