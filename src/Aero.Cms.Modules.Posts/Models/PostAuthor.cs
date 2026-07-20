using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents persisted author profile data referenced by blog posts.
/// </summary>
public class PostAuthor : SableDocument, IAuditable
{
    /// <summary>
    /// Gets or sets the public display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional public biography.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Gets or sets the optional contact email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the optional avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    // IAuditable
    /// <summary>
    /// Gets or sets when the author profile was created.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the author profile was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that created the profile.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that last modified the profile.
    /// </summary>
    public string? ModifiedBy { get; set; }
}
