using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Provider-neutral, one-time manager callback state.</summary>
public sealed class ManagerAuthenticationState : SableDocument, IAuditable, IVersioned
{
    public const string LinkRecoveryAdministratorPurpose = "link_recovery_administrator";
    public const string SignInPurpose = "manager_sign_in";

    public long AuthorityBindingId { get; set; }
    public long? LinkIntentId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string SecretDigest { get; set; } = string.Empty;
    public string CallbackUri { get; set; } = string.Empty;
    public string ReturnPath { get; set; } = "/manager";
    public string ProtectedProviderCorrelation { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
