using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Installation-wide, non-secret manager identity authority configuration.</summary>
public sealed class ManagerIdentityAuthorityBinding : SableDocument, IAuditable, IVersioned
{
    public const string InstallationSingletonKey = "aerocms.manager-identity-authority";

    /// <summary>Constant discriminator enforcing one authority document per installation.</summary>
    public string SingletonKey { get; set; } = InstallationSingletonKey;
    public string Provider { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string BindingKey { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    /// <summary>Canonical public HTTPS origin used for every manager federation callback.</summary>
    public string PublicOrigin { get; set; } = string.Empty;
    public long VaultId { get; set; }
    public string VaultEnvironment { get; set; } = string.Empty;
    public string CredentialPath { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; }
    public long? VerifiedByUserId { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    /// <summary>Gets or sets when this authority became the effective manager sign-in authority.</summary>
    public DateTimeOffset? ActivatedAtUtc { get; set; }
    /// <summary>Gets or sets the recovery administrator who explicitly activated this authority.</summary>
    public long? ActivatedByRecoveryAdministratorUserId { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
