using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a single latest-draft snapshot of a page.
/// One per page — upsert replaces the entire draft atomically.
/// Cleaned up when the page is manually saved or published.
/// </summary>
public sealed class PageDraft : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long PageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public List<EditorBlock> Blocks { get; set; } = [];
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset DraftedAt { get; set; }
}
