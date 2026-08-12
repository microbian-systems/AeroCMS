using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Describes one site-scoped source of first-class CMS documents that may be
/// selected by a dynamic content field.
/// </summary>
public interface IContentReferenceSourceProvider
{
    /// <summary>Stable value persisted with the selected identifier.</summary>
    string SourceKey { get; }

    /// <summary>Human-readable source name shown in the manager.</summary>
    string DisplayName { get; }

    /// <summary>Returns a bounded set of current-site options.</summary>
    Task<Result<IReadOnlyList<CmsContentReferenceOption>>> SearchAsync(
        long siteId,
        string? culture,
        string? search,
        int take,
        CancellationToken ct = default);

    /// <summary>Checks whether one identifier belongs to this source and site.</summary>
    Task<Result<bool>> ExistsAsync(
        long siteId,
        long id,
        CancellationToken ct = default);
}

/// <summary>A registered CMS document source exposed to manager clients.</summary>
public sealed record CmsContentReferenceSource(
    string Key,
    string DisplayName);

/// <summary>A bounded manager-facing option from a CMS document source.</summary>
public sealed record CmsContentReferenceOption(
    string Id,
    string Title,
    string Slug,
    string Culture);

/// <summary>
/// Typed value persisted for a Page, Post, or Docs reference. Snowflake IDs
/// remain strings across the JSON/browser boundary.
/// </summary>
public sealed record CmsContentReferenceValue(
    string Source,
    string Id);

/// <summary>A site-scoped selector option for a provider-qualified virtual entry.</summary>
public sealed record ContentEntryReferenceOption(
    string Provider,
    string StableId,
    string Title,
    string? Detail = null);
