using System.Text.Json.Serialization;
using Aero.Core.Data;
using AeroDB.Sable;
using Aero.Auth.Services;

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
    /// Gets or sets when the API key expires. Null means it never expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the API key was revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }

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
