using System.Security.Claims;
using Aero.Cms.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Identity;

/// <summary>Parses only the strict claim shape emitted for external-member sessions.</summary>
public static class ExternalMemberPrincipal
{
    /// <summary>Creates a claims principal suitable for the external-member cookie.</summary>
    public static ClaimsPrincipal Create(long memberId, string provider, long sessionId, long securityVersion, string? displayName = null)
    {
        if (memberId <= 0 || sessionId <= 0 || securityVersion <= 0 ||
            !ExternalMemberSessionProviders.IsSupported(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(memberId), "External-member session values must be valid local values.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, memberId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ExternalMemberClaimTypes.PrincipalKind, ExternalMemberClaimTypes.ExternalMember),
            new(ExternalMemberClaimTypes.AuthenticationProvider, provider.Trim()),
            new(ExternalMemberClaimTypes.SessionId, sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ExternalMemberClaimTypes.SecurityVersion, securityVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(displayName))
            claims.Add(new Claim(ClaimTypes.Name, displayName.Trim()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, ExternalMemberAuthenticationDefaults.Scheme));
    }

    /// <summary>Attempts to parse an unambiguous, non-privileged external-member claim set.</summary>
    public static bool TryRead(ClaimsPrincipal principal, out ExternalMemberSessionClaims claims)
    {
        claims = default;
        var identities = principal.Identities.ToArray();
        if (identities.Length != 1 ||
            !string.Equals(identities[0].AuthenticationType, ExternalMemberAuthenticationDefaults.Scheme, StringComparison.Ordinal) ||
            !identities[0].IsAuthenticated ||
            HasForbiddenClaims(principal))
            return false;

        if (!TryGetExactlyOne(principal, ClaimTypes.NameIdentifier, out var memberIdText) ||
            !TryGetExactlyOne(principal, ExternalMemberClaimTypes.PrincipalKind, out var kind) ||
            !TryGetExactlyOne(principal, ExternalMemberClaimTypes.AuthenticationProvider, out var provider) ||
            !TryGetExactlyOne(principal, ExternalMemberClaimTypes.SessionId, out var sessionIdText) ||
            !TryGetExactlyOne(principal, ExternalMemberClaimTypes.SecurityVersion, out var versionText) ||
            !string.Equals(kind, ExternalMemberClaimTypes.ExternalMember, StringComparison.Ordinal) ||
            !ExternalMemberSessionProviders.IsSupported(provider) ||
            !long.TryParse(memberIdText, out var memberId) || memberId <= 0 ||
            !long.TryParse(sessionIdText, out var sessionId) || sessionId <= 0 ||
            !long.TryParse(versionText, out var securityVersion) || securityVersion <= 0)
        {
            return false;
        }

        claims = new ExternalMemberSessionClaims(memberId, provider, sessionId, securityVersion);
        return true;
    }

    private static bool TryGetExactlyOne(ClaimsPrincipal principal, string type, out string value)
    {
        var values = principal.FindAll(type).Select(claim => claim.Value).ToArray();
        value = values.Length == 1 ? values[0] : string.Empty;
        return values.Length == 1;
    }

    private static bool HasForbiddenClaims(ClaimsPrincipal principal) => principal.Claims.Any(claim =>
        claim.Type == ClaimTypes.Role ||
        string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claim.Type, "is_admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Contains the trusted values parsed from a strict external-member cookie principal.</summary>
public readonly record struct ExternalMemberSessionClaims(long MemberId, string Provider, long SessionId, long SecurityVersion);

/// <summary>Exposes the strict external-member principal to endpoint handlers.</summary>
public sealed class CurrentPrincipal(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipal
{
    private readonly ExternalMemberSessionClaims? _externalClaims = ReadExternalClaims(httpContextAccessor.HttpContext?.User);

    public bool IsAuthenticated => _externalClaims is not null;
    public long? PrincipalId => _externalClaims?.MemberId;
    public PrincipalKind? Kind => _externalClaims is null ? null : PrincipalKind.ExternalMember;
    public string? AuthenticationProvider => _externalClaims?.Provider;
    public long? ExternalSessionId => _externalClaims?.SessionId;
    public long? SecurityVersion => _externalClaims?.SecurityVersion;

    private static ExternalMemberSessionClaims? ReadExternalClaims(ClaimsPrincipal? principal) =>
        principal is not null && ExternalMemberPrincipal.TryRead(principal, out var claims) ? claims : null;
}
