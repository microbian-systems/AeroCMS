using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents an author of blog posts.
/// </summary>
public class PostAuthor : SableDocument, IAuditable
{
    /// <summary>
    /// Gets or sets the name of the author.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the biography of the author.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Gets or sets the email address of the author.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the URL of the author's avatar image.
    /// </summary>
    public string? AvatarUrl { get; set; }

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
