namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Represents a record for RedirectRule.
/// </summary>
public record RedirectRule
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public long Id { get; init; }
        /// <summary>
    /// Gets or sets the From Path.
    /// </summary>
public required string FromPath { get; init; }
        /// <summary>
    /// Gets or sets the To Path.
    /// </summary>
public required string ToPath { get; init; }
        /// <summary>
    /// Gets or sets the Status Code.
    /// </summary>
public int StatusCode { get; init; } = 301;
        /// <summary>
    /// Gets or sets the Created At.
    /// </summary>
public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
