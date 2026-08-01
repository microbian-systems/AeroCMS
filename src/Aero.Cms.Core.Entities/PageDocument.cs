using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Html;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;


/// <summary>
/// Stores a site- and culture-specific page, its navigation metadata, and draft/public HTML snapshots.
/// </summary>
/// <remarks>
/// This mutable document carries identifiers and lifecycle fields but does not itself enforce site isolation, slug
/// normalization, hierarchy consistency, validation, or persistence. Content replacement and publication clone the
/// supplied/current tree to avoid sharing it with the draft at that moment; <see cref="PublishedContent"/> remains
/// publicly mutable afterwards.
/// </remarks>
public sealed class PageDocument : SableDocument, IAuditable, ISiteOwned, ISoftDeleted, IAuditableEntity
{
        /// <summary>
    /// Gets or sets the site identifier recorded on this page.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the optional group identifier linking culture variants.
    /// </summary>
public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets the optional source-page identifier for a derived translation.
    /// </summary>
public long? SourcePageId { get; set; }
        /// <summary>
    /// Gets or sets the culture label; this type does not normalize or validate it.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the page kind used by consuming code.
    /// </summary>
public PageKind Kind { get; set; } = PageKind.Standard;
    /// <summary>
    /// Gets or sets the stable page-rendering strategy identifier.
    /// </summary>
public string RendererId { get; set; } = PageRendererIds.AeroComposition;
        /// <summary>
    /// Gets or sets the append-only source version currently associated with the editable draft.
    /// </summary>
public long? DraftSourceVersionId { get; set; }
        /// <summary>
    /// Gets or sets the source version captured by the most recent publication.
    /// </summary>
public long? PublishedSourceVersionId { get; set; }
        /// <summary>
    /// Gets or sets the route slug; normalization and uniqueness are external concerns.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the display title.
    /// </summary>
public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the optional summary.
    /// </summary>
public string? Summary { get; set; }
        /// <summary>
    /// Gets or sets the optional SEO title.
    /// </summary>
public string? SeoTitle { get; set; }
        /// <summary>
    /// Gets or sets the optional SEO description.
    /// </summary>
public string? SeoDescription { get; set; }

    /// <summary>
    /// Gets or sets whether the published page is eligible for the site's search index.
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the published page may be used to ground public AI answers.
    /// Search inclusion is also required.
    /// </summary>
    public bool IncludeInPublicAi { get; set; }

    // ── Hierarchy ───────────────────────────────────────────────────────

    /// <summary>
    /// Parent page ID. <c>null</c> for root-level pages.
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// Full materialized path (e.g. "/sports/basketball/nba").
    /// </summary>
    public string Path { get; set; } = "/";

    /// <summary>
    /// Distance from root. 0 = root, 1 = direct child, etc.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Display order among siblings. Lower = first. Insertions require renumbering.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// When <c>true</c>, this page and all descendants are hidden from navigation menus.
    /// </summary>
    public bool IsHidden { get; set; }

    // ── Layout ───────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the editable HTML fragment owned by this page aggregate.
    /// </summary>
    public HtmlPageContent DraftContent { get; set; } = new();

    /// <summary>
    /// Gets or sets the cloned published HTML snapshot available to public rendering.
    /// </summary>
    public HtmlPageContent? PublishedContent { get; set; }

    /// <summary>
    /// Gets or sets optional typed-content meaning attached to stable nodes in <see cref="DraftContent"/>.
    /// </summary>
    public PageCompositionDocument DraftComposition { get; set; } = new();

    /// <summary>
    /// Gets or sets the published composition snapshot paired with <see cref="PublishedContent"/>.
    /// </summary>
    public PageCompositionDocument? PublishedComposition { get; set; }

    /// <summary>
    /// Replaces the editable draft with an independent validated snapshot.
    /// Validation is performed by the Pages application boundary before this mutation.
    /// </summary>
    /// <param name="content">The non-null validated content to clone into the draft.</param>
    /// <param name="modifiedOn">The timestamp to record as the last modification time; callers conventionally supply UTC.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="OverflowException">Incrementing <see cref="ContentRevision"/> exceeds <see cref="long.MaxValue"/>.</exception>
    public void ReplaceDraftContent(HtmlPageContent content, DateTimeOffset modifiedOn)
        => ReplaceDraftContent(content, DraftComposition, modifiedOn);

    /// <summary>
    /// Replaces the editable HTML and composition sidecar as one independent draft snapshot.
    /// Validation is performed by the Pages application boundary before this mutation.
    /// </summary>
    /// <param name="content">The non-null validated HTML content.</param>
    /// <param name="composition">The non-null validated composition sidecar.</param>
    /// <param name="modifiedOn">The timestamp to record as the last modification time.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="content"/> or <paramref name="composition"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OverflowException">Incrementing <see cref="ContentRevision"/> exceeds <see cref="long.MaxValue"/>.</exception>
    public void ReplaceDraftContent(
        HtmlPageContent content,
        PageCompositionDocument composition,
        DateTimeOffset modifiedOn)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(composition);

        DraftContent = HtmlTreeOperations.ClonePreservingNodeIds(content);
        DraftComposition = composition.CreateSnapshot();
        ContentRevision = checked(ContentRevision + 1);
        ModifiedOn = modifiedOn;
    }

    /// <summary>
    /// Publishes an independent snapshot of the current validated draft.
    /// </summary>
    /// <param name="publishedOn">The timestamp to record for publication and modification; callers conventionally supply UTC.</param>
    /// <exception cref="OverflowException">Incrementing <see cref="PublishedVersion"/> exceeds <see cref="long.MaxValue"/>.</exception>
    public void PublishDraftContent(DateTimeOffset publishedOn)
    {
        PublishedContent = HtmlTreeOperations.ClonePreservingNodeIds(DraftContent);
        PublishedComposition = DraftComposition.CreateSnapshot();
        PublishedSourceVersionId = DraftSourceVersionId;
        PublishedContentRevision = ContentRevision;
        PublicationState = ContentPublicationState.Published;
        PublishedOn = publishedOn;
        PublishedVersion = checked(PublishedVersion + 1);
        ModifiedOn = publishedOn;
    }

    /// <summary>
    /// Removes public availability while preserving the last published snapshot.
    /// </summary>
    /// <param name="modifiedOn">The timestamp to record as the last modification time; callers conventionally supply UTC.</param>
    public void UnpublishContent(DateTimeOffset modifiedOn)
    {
        PublicationState = ContentPublicationState.Draft;
        PublishedOn = null;
        ModifiedOn = modifiedOn;
    }

    /// <summary>
    /// Latest draft content revision generated by the composition pipeline.
    /// This is distinct from AeroDB optimistic concurrency metadata.
    /// </summary>
    public long ContentRevision { get; set; }

    /// <summary>
    /// Gets or sets the draft content revision captured by the most recent publication.
    /// </summary>
    public long PublishedContentRevision { get; set; }

        /// <summary>
    /// Gets or sets the lifecycle state used by public-visibility checks.
    /// </summary>
public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
        /// <summary>
    /// Gets or sets the timestamp assigned by <see cref="PublishDraftContent"/>; its offset is not normalized by this type.
    /// </summary>
public DateTimeOffset? PublishedOn { get; set; } = null;

    /// <summary>
    /// Monotonic counter incremented on every publish.
    /// </summary>
    public long PublishedVersion { get; set; }

        /// <summary>
    /// Gets whether the page is published and not soft deleted.
    /// </summary>
    [JsonIgnore]
    public bool IsPubliclyVisible =>
        PublicationState == ContentPublicationState.Published && !Deleted;

    /// <summary>
    /// Gets whether the currently editable content or source differs from the last published snapshot.
    /// </summary>
    [JsonIgnore]
    public bool HasUnpublishedChanges =>
        PublicationState == ContentPublicationState.Published
        && (ContentRevision != PublishedContentRevision
            || DraftSourceVersionId != PublishedSourceVersionId);

    /// <summary>
    /// Gets or sets whether this page should be displayed in the main navigation menu.
    /// </summary>
    public bool ShowInNavMenu { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the global header navigation should be shown when viewing this page.
    /// </summary>
    public bool ShowHeaderNavigation { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional image URL to be used as a background for the page header/hero section.
    /// </summary>
    public string? HeaderImageUrl { get; set; }

    /// <summary>
    ///  Flag to hide the header on the page
    /// </summary>
    public bool HideHeader { get; set; } = false;
    /// <summary>
    ///  Flag to hide the footer on the page
    /// </summary>
    public bool HideFooter { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the chat agent widget should be shown on this page.
    /// </summary>
    public bool ShowChatAgent { get; set; } = true;

    // ── Soft Delete ──────────────────────────────────────────────────────

    /// <summary>
    /// <c>true</c> when the page has been soft-deleted via AeroDB.
    /// Managed automatically by AeroDB's <c>ISoftDeleted</c> policy.
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Timestamp of soft deletion. Managed automatically by AeroDB.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // IAuditable
    /// <summary>Gets or sets the creation timestamp. The default is UTC, but setters do not enforce an offset.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the last-modified timestamp; callers and persistence conventionally use UTC, but setters do not enforce it.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the actor recorded as creating this document, when available.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the actor recorded as last modifying this document, when available.</summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Maps this document to a <see cref="PageViewModel"/> for Wolverine
    /// message bus publishing.  Avoids exposing the internal PageDocument
    /// type to downstream consumers.
    /// </summary>
    /// <returns>A transport model containing page metadata and serialized draft and published content snapshots.</returns>
    public PageViewModel ToViewModel() => new()
    {
        Id = Id,
        Title = Title,
        Slug = Slug,
        Kind = Kind,
        RendererId = PageRendererIds.NormalizeOrDefault(RendererId),
        Summary = Summary,
        SeoTitle = SeoTitle,
        SeoDescription = SeoDescription,
        IncludeInSearch = IncludeInSearch,
        IncludeInPublicAi = IncludeInPublicAi,
        PublishedOn = PublishedOn,
        IsPublished = PublicationState == ContentPublicationState.Published,
        PublicationState = PublicationState,
        SiteId = SiteId,
        Culture = Culture,
        TranslationGroupId = TranslationGroupId,
        ParentId = ParentId,
        Path = Path,
        Depth = Depth,
        Order = Order,
        IsHidden = IsHidden,
        ShowInNavMenu = ShowInNavMenu,
        ShowHeaderNavigation = ShowHeaderNavigation,
        HideFooter = HideFooter,
        ShowChatAgent = ShowChatAgent,
        ContentRevision = ContentRevision,
        HasUnpublishedChanges = HasUnpublishedChanges,
        DraftContentJson = JsonSerializer.Serialize(DraftContent, HtmlJsonContext.Default.HtmlPageContent),
        PublishedContentJson = PublishedContent is null
            ? null
            : JsonSerializer.Serialize(PublishedContent, HtmlJsonContext.Default.HtmlPageContent),
        DraftCompositionJson = JsonSerializer.Serialize(
            DraftComposition,
            PageCompositionJsonContext.Default.PageCompositionDocument),
        PublishedCompositionJson = PublishedComposition is null
            ? null
            : JsonSerializer.Serialize(
                PublishedComposition,
                PageCompositionJsonContext.Default.PageCompositionDocument)
    };
}
