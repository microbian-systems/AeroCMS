using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Contains only non-secret authority configuration accepted from a manager.</summary>
public sealed record ConfigureExternalIdentityAuthorityRequest(
    string Provider,
    string OrganizationId,
    string Authority,
    long VaultId,
    string VaultEnvironment,
    bool Enabled)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Exposes only the safe, non-secret authority state needed by the manager UI.</summary>
public sealed record ExternalIdentityAuthorityState(
    bool Configured,
    string? Provider,
    string? Authority,
    string? OrganizationId,
    long? VaultId,
    string? VaultEnvironment,
    bool Enabled);

/// <summary>Creates a selected-site invitation without accepting scope or provider identifiers.</summary>
public sealed record CreateExternalIdentityInvitationRequest(string Email, DateTimeOffset ExpiresAt)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Returns the one-time opaque invitation handle and its expiry.</summary>
public sealed record ExternalIdentityInvitationResponse(string Handle, DateTimeOffset ExpiresAt);

/// <summary>Manager-selected expiry for a local member password reset.</summary>
public sealed record IssueLocalExternalMemberPasswordResetAdminRequest(DateTimeOffset ExpiresAt)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Returns the one-time reset handle exactly once.</summary>
public sealed record LocalExternalMemberPasswordResetResponse(string Handle, DateTimeOffset ExpiresAt);
