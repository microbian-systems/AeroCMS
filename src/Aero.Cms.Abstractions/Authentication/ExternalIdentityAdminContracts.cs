using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Contains the browser-safe antiforgery handshake material for one administration request.</summary>
public sealed record ExternalIdentityAntiforgeryResponse(string HeaderName, string RequestToken);

/// <summary>Provides reflection-free JSON metadata for external identity administration handshake contracts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ExternalIdentityAntiforgeryResponse))]
public partial class ExternalIdentityAdminJsonContext : JsonSerializerContext
{
}

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

/// <summary>Requests an activity transition using the member's currently observed security version.</summary>
public sealed record ExternalMemberLifecycleRequest
{
    /// <summary>Gets the explicitly supplied next activity state; omission is rejected at deserialization.</summary>
    [JsonRequired]
    public required bool IsActive { get; init; }

    /// <summary>Gets the explicitly supplied currently observed member security version.</summary>
    [JsonRequired]
    public required long ExpectedSecurityVersion { get; init; }

    /// <summary>Gets an optional opaque operation-correlation value.</summary>
    public string? CorrelationId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Returns only browser-safe lifecycle state after a durable transition.</summary>
public sealed record ExternalMemberLifecycleResponse(
    long MemberId,
    bool IsActive,
    long SecurityVersion,
    DateTimeOffset? ModifiedOn);
