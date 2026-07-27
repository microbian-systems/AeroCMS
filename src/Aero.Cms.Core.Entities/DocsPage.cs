using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Stores a site- and culture-specific documentation page with markdown and publication metadata.
/// </summary>
public sealed class DocsPage : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the site identifier recorded with this page; isolation is not enforced by the entity.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets an optional identifier grouping culture variants.
    /// </summary>
public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets the stored culture label, initialized to <c>en-US</c> without normalization.
    /// </summary>
public string Culture { get; set; } = "en-US";
        /// <summary>
    /// Gets or sets the stored route slug; validation and uniqueness are external concerns.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the required-initialized display title.
    /// </summary>
public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets optional summary text.
    /// </summary>
public string? Summary { get; set; }
        /// <summary>
    /// Gets or sets optional markdown source; rendering and sanitization are outside this entity.
    /// </summary>
public string? MarkdownContent { get; set; }
        /// <summary>
    /// Gets or sets optional SEO title metadata.
    /// </summary>
public string? SeoTitle { get; set; }
        /// <summary>
    /// Gets or sets optional SEO description metadata.
    /// </summary>
public string? SeoDescription { get; set; }

    /// <summary>
    /// Gets or sets whether the published document is eligible for the site's search index.
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the published document may be used to ground public AI answers.
    /// </summary>
    public bool IncludeInPublicAi { get; set; }

        /// <summary>
    /// Gets or sets the stored publication state; changing it has no side effects here.
    /// </summary>
public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
        /// <summary>
    /// Gets or sets an optional publication timestamp; its offset is not normalized by this type.
    /// </summary>
public DateTimeOffset? PublishedOn { get; set; } = null;
        /// <summary>
    /// Gets whether the stored publication state is <c>Published</c>; no other visibility rules are evaluated.
    /// </summary>
    [JsonIgnore]
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

    /// <summary>
    /// Monotonic counter incremented on every publish.
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

    // ── IAuditable ─────────────────────────────────────────────────────────

    /// <summary>Gets or sets the creation timestamp. The default is UTC, but setters do not enforce an offset.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the last-modified timestamp; callers and persistence conventionally use UTC, but setters do not enforce it.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the actor recorded as creating this document, when available.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the actor recorded as last modifying this document, when available.</summary>
    public string? ModifiedBy { get; set; }

    // ── Mapping ──────────────────────────────────────────────────────────

    /// <summary>
    /// Maps this document to a <see cref="DocViewModel"/> for Wolverine
    /// message bus publishing and Orleans grain transport.
    /// Mirroring <see cref="PageDocument.ToViewModel()"/>.
    /// </summary>
    /// <returns>A transport model containing this document's current metadata and content values.</returns>
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
        IncludeInSearch = IncludeInSearch,
        IncludeInPublicAi = IncludeInPublicAi,
        PublicationState = PublicationState,
        PublishedOn = PublishedOn,
        ShowHeaderNavigation = ShowHeaderNavigation,
        HeaderImageUrl = HeaderImageUrl,
        ParentId = ParentId,
        Order = Order,
        PublishedVersion = PublishedVersion,
        CreatedOn = CreatedOn,
        ModifiedOn = ModifiedOn,
        CreatedBy = CreatedBy,
        ModifiedBy = ModifiedBy
    };
}
