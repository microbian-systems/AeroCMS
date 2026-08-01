using System.Text.Json.Serialization;
using Aero.Core.Data;
using AeroDB.Sable;
using Aero.Auth.Services;
using Aero.Cms.Abstractions.Security;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a persisted API key document in AeroDB.
/// </summary>
public sealed class ApiKeyDocument : SableDocument, IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier of the user this API key belongs to.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Gets or sets the tenant boundary for this key. User-session bootstrap keys use zero.
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Gets or sets the sites this key may address.
    /// </summary>
    public List<long> AllowedSiteIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this is a login bootstrap key or an explicitly scoped service key.
    /// </summary>
    public AeroApiKeyCredentialKind CredentialKind { get; set; }

    /// <summary>
    /// Gets or sets the hashed secret of the API key.
    /// Only the hash is stored, never the raw key.
    /// </summary>
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a friendly name for the API key.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment this API key is for (Test, Live).
    /// </summary>
    public ApiKeyEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets whether the key can authenticate to the MCP transport.
    /// </summary>
    public bool McpServer { get; set; }

    /// <summary>
    /// Gets or sets whether the key has all registered domain operations inside its own scope.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Gets or sets canonical <c>domain:operations</c> capability values.
    /// </summary>
    public List<string> Permissions { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional named rate-limit profile.
    /// </summary>
    public string? RateLimitPolicy { get; set; }

    /// <summary>
    /// Gets or sets when the API key expires. Null means it never expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the API key was revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets when this key was last successfully validated.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Gets or sets when a replacement credential was issued.
    /// </summary>
    public DateTimeOffset? RotatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user identifier that revoked this key.
    /// </summary>
    public long? RevokedByUserId { get; set; }

    /// <summary>
    /// Gets a value indicating whether the API key is active.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTimeOffset.UtcNow);

    // IAuditable
    /// <summary>Gets or sets the creation timestamp. The default is UTC, but setters do not enforce an offset.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the last-modified timestamp; callers and persistence conventionally use UTC, but setters do not enforce it.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the actor recorded as creating this document, when available.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the actor recorded as last modifying this document, when available.</summary>
    public string? ModifiedBy { get; set; }
}
