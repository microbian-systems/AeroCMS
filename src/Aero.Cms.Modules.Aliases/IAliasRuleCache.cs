using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Provides a process-local, read-only snapshot for resolving aliases by site,
/// culture, and path. Implementations normalize lookup inputs with
/// <see cref="AliasDocument.NormalizeCulture(string?)"/> and
/// <see cref="AliasDocument.NormalizePath(string?)"/>.
/// <para>
/// A lookup never performs I/O. Callers that require request-time recovery from
/// an empty or stale snapshot must provide their own persistence fallback.
/// </para>
/// </summary>
public interface IAliasRuleCache
{
    /// <summary>
    /// Finds the alias for the supplied site, culture, and old path.
    /// Inputs are normalized before lookup; returns <see langword="null"/> when
    /// the current snapshot has no matching entry.
    /// </summary>
    AliasRuleEntry? Find(long siteId, string culture, string oldPath);

    /// <summary>
    /// Rebuilds the snapshot from persisted aliases. Implementations serialize
    /// concurrent refreshes and leave the last usable snapshot in place when a
    /// refresh cannot complete. Cancellation while acquiring the refresh gate
    /// propagates to the caller. Once the gate is acquired, cancellation during
    /// persistence loading is caught and logged like other refresh failures.
    /// </summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>
    /// Discards the current snapshot immediately. This operation does not
    /// trigger a refresh or database access.
    /// </summary>
    void Invalidate();
}

/// <summary>
/// Immutable redirect data stored in an alias-cache snapshot.
/// </summary>
/// <param name="SiteId">The site that owns the alias.</param>
/// <param name="Culture">The normalized culture that scopes the alias.</param>
/// <param name="OldPath">The normalized path used for lookup.</param>
/// <param name="NewPath">The normalized redirect target path.</param>
/// <param name="StatusCode">The HTTP redirect status code.</param>
public sealed record AliasRuleEntry(
    long SiteId,
    string Culture,
    string OldPath,
    string NewPath,
    int StatusCode = 301
);

/// <summary>
/// Composite, normalized key for an alias-cache entry. The culture and path
/// components are case-insensitive only to the extent supplied normalization
/// makes them so.
/// </summary>
/// <param name="SiteId">The site that owns the alias.</param>
/// <param name="Culture">The normalized culture component.</param>
/// <param name="Path">The normalized old-path component.</param>
public readonly record struct SitePathKey(long SiteId, string Culture, string Path);
