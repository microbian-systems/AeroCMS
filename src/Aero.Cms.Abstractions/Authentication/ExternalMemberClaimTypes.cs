namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Defines application-owned claims carried only by the external-member cookie.</summary>
public static class ExternalMemberClaimTypes
{
    public const string PrincipalKind = "aero_principal_kind";
    public const string AuthenticationProvider = "aero_auth_provider";
    public const string SessionId = "aero_external_session_id";
    public const string SecurityVersion = "aero_security_version";
    public const string ExternalMember = "external_member";
}
