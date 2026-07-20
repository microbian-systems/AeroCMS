namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Identifies the local kind of the authenticated AeroCMS principal.</summary>
public enum PrincipalKind
{
    /// <summary>An ASP.NET Core Identity user used to operate the CMS.</summary>
    InternalUser,

    /// <summary>A separate storefront customer or partner member.</summary>
    ExternalMember
}

/// <summary>Provides the validated local principal for the current request.</summary>
public interface ICurrentPrincipal
{
    /// <summary>Gets whether a recognized local principal is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Gets the local Snowflake identifier when the principal is recognized.</summary>
    long? PrincipalId { get; }

    /// <summary>Gets the local principal kind when the principal is recognized.</summary>
    PrincipalKind? Kind { get; }

    /// <summary>Gets the application authentication provider for the current principal.</summary>
    string? AuthenticationProvider { get; }

    /// <summary>Gets the external session identifier for an external member.</summary>
    long? ExternalSessionId { get; }

    /// <summary>Gets the local security version carried by the authenticated principal.</summary>
    long? SecurityVersion { get; }
}
