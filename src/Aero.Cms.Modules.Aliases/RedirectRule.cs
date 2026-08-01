namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Describes a standalone redirect rule model. It is not registered by this
/// module's persistence, cache, rewrite rule, or endpoint mappings; current
/// alias behavior uses <see cref="Aero.Cms.Core.Entities.AliasDocument"/> instead.
/// </summary>
public record RedirectRule
{
        /// <summary>
    /// Gets the rule identifier.
    /// </summary>
public long Id { get; init; }
        /// <summary>
    /// Gets the source path. This model does not normalize or validate it.
    /// </summary>
public required string FromPath { get; init; }
        /// <summary>
    /// Gets the redirect target path. This model does not normalize or validate it.
    /// </summary>
public required string ToPath { get; init; }
        /// <summary>
    /// Gets the HTTP status code to use; defaults to 301.
    /// </summary>
public int StatusCode { get; init; } = 301;
        /// <summary>
    /// Gets the creation timestamp assigned when the record is initialized.
    /// </summary>
public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
