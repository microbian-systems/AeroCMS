using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a class for DocsPage.
/// </summary>
public sealed class DocsPage : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Translation Group Id.
    /// </summary>
public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = "en-US";
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Summary.
    /// </summary>
public string? Summary { get; set; }
        /// <summary>
    /// Gets or sets the Markdown Content.
    /// </summary>
public string? MarkdownContent { get; set; }
        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
public string? SeoTitle { get; set; }
        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
public string? SeoDescription { get; set; }
    
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
public DateTimeOffset? PublishedOn { get; set; } = null;
        /// <summary>
    /// Gets or sets the Is Publicly Visible.
    /// </summary>
public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

    /// <summary>
    /// Monotonic counter incremented on every publish.
    /// Compared against <see cref="DocsEditorState.DraftVersion"/> in the admin
    /// service layer to detect unpublished changes.
    /// </summary>
    public long PublishedVersion { get; set; }

    /// <summary>
    /// Gets or sets whether the global header navigation should be shown when viewing this page.
    /// </summary>
    public bool ShowHeaderNavigation { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional image URL to be used as a background for the page header/hero section.
    /// </summary>
    public string? HeaderImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the parent document ID for hierarchical structure.
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the sort order among siblings.
    /// </summary>
    public int Order { get; set; }

    // ── Published block layout ──────────────────────────────────────────

    /// <summary>
    /// Published layout manifest: regions → columns → block placements.
    /// Built from <see cref="DocsEditorState"/> on publish.
    /// Rendered SSR by LayoutRegionRenderer components during public page rendering.
    /// </summary>
    public List<LayoutRegion> LayoutRegions { get; set; } = [];

    // ── Block schema versioning ─────────────────────────────────────────

    /// <summary>
    /// Tracks the block content schema version. Incremented by migration when
    /// legacy block content is transformed into Neo blocks. Used for idempotency.
    /// Mirroring <c>PageDocument.BlockSchemaVersion</c>.
    /// </summary>
    public int BlockSchemaVersion { get; set; }

    // ── IAuditable ─────────────────────────────────────────────────────────

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    // ── Mapping ──────────────────────────────────────────────────────────

    /// <summary>
    /// Maps this document to a <see cref="DocViewModel"/> for Wolverine
    /// message bus publishing and Orleans grain transport.
    /// Mirroring <see cref="PageDocument.ToViewModel()"/>.
    /// </summary>
    public DocViewModel ToViewModel() => new()
    {
        Id = Id,
        SiteId = SiteId,
        TranslationGroupId = TranslationGroupId,
        Culture = Culture,
        Slug = Slug,
        Title = Title,
        Summary = Summary,
        MarkdownContent = MarkdownContent,
        SeoTitle = SeoTitle,
        SeoDescription = SeoDescription,
        PublicationState = PublicationState,
        PublishedOn = PublishedOn,
        ShowHeaderNavigation = ShowHeaderNavigation,
        HeaderImageUrl = HeaderImageUrl,
        ParentId = ParentId,
        Order = Order,
        PublishedVersion = PublishedVersion,
        BlockSchemaVersion = BlockSchemaVersion,
        CreatedOn = CreatedOn,
        ModifiedOn = ModifiedOn,
        CreatedBy = CreatedBy,
        ModifiedBy = ModifiedBy
    };
}
