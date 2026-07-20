namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Names the isolated authentication scheme used by storefront members.</summary>
public static class ExternalMemberAuthenticationDefaults
{
    /// <summary>The non-default cookie authentication scheme for external members.</summary>
    public const string Scheme = "AeroCms.ExternalMember";

    /// <summary>The authorization policy that admits only a validated external-member cookie.</summary>
    public const string Policy = "external-member";

    /// <summary>The policy that requires an active membership for the host-resolved public site.</summary>
    public const string SitePolicy = "external-member:site";
}
