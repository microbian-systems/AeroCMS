using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Authorizes one exact provider organization as the external authority for a tenant.</summary>
public sealed class ExternalOrganizationBinding : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string BindingKey { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public long VaultId { get; set; }
    public string VaultEnvironment { get; set; } = string.Empty;
    public string CredentialPath { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
