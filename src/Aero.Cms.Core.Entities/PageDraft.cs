using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a single latest-draft snapshot of a page.
/// One per page — upsert replaces the entire draft atomically.
/// Cleaned up when the page is manually saved or published.
/// </summary>
public sealed class PageDraft : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Page Id.
    /// </summary>
public long PageId { get; set; }
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Summary.
    /// </summary>
public string? Summary { get; set; }
        /// <summary>
    /// Gets or sets the Root Node Json.
    /// </summary>
public string? RootNodeJson { get; set; }
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
        /// <summary>
    /// Gets or sets the Drafted At.
    /// </summary>
    public DateTimeOffset DraftedAt { get; set; }

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
