using System.Security.Claims;
using Aero.Core.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.RateLimiting;

/// <summary>
/// Creates bounded server-derived partition keys without embedding secrets or PII.
/// </summary>
public static class AeroRateLimitPartitionKeyFactory
{
    private const string UnknownScope = "0";
    private const string UnknownClient = "unknown";
    private const int MaximumSegmentLength = 128;

    public static string Create(HttpContext httpContext, string policyName)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        var siteContext = httpContext.RequestServices.GetService<ISiteContext>();
        var tenantId = siteContext?.TenantId > 0
            ? siteContext.TenantId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : UnknownScope;
        var siteId = siteContext?.SiteId > 0
            ? siteContext.SiteId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : UnknownScope;

        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var apiKeyId = FirstClaim(principal, "api_key_id", "api-key-id", "key_id");
            if (!string.IsNullOrWhiteSpace(apiKeyId))
                return Join(tenantId, siteId, "api-key", apiKeyId, policyName);

            var principalId = FirstClaim(
                principal,
                ClaimTypes.NameIdentifier,
                "sub",
                "user_id");
            return Join(
                tenantId,
                siteId,
                "principal",
                principalId ?? UnknownClient,
                policyName);
        }

        var remoteAddress = httpContext.Connection.RemoteIpAddress;
        if (remoteAddress?.IsIPv4MappedToIPv6 == true)
            remoteAddress = remoteAddress.MapToIPv4();

        return Join(
            tenantId,
            siteId,
            "anonymous-ip",
            remoteAddress?.ToString() ?? UnknownClient,
            policyName);
    }

    private static string? FirstClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string Join(params string[] segments)
        => string.Join(
            '|',
            segments.Select(segment =>
            {
                var normalized = segment.Trim().Replace('|', '_');
                return normalized.Length <= MaximumSegmentLength
                    ? normalized
                    : normalized[..MaximumSegmentLength];
            }));
}
