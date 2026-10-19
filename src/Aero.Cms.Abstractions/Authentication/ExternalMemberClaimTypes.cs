namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Defines application-owned claims carried only by the external-member cookie.</summary>
public static class ExternalMemberClaimTypes
{
    public const string PrincipalKind = "aero_principal_kind";
    public const string AuthenticationProvider = "aero_auth_provider";
    public const string SessionId = "aero_external_session_id";
    public const string SecurityVersion = "aero_security_version";
    public const string ExternalMember = "external_member";
    public const string TenantId = "aero_tenant_id";
    public const string SiteId = "aero_site_id";
    public const string TokenUse = "aero_token_use";
    public const string AccessTokenUse = "external_member_access";
}
