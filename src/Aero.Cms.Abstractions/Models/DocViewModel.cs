using Aero.Cms.Abstractions.Enums;
using Aero.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Abstractions.Models;

public sealed record DocViewModel : EntityViewModel
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? MarkdownContent { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }

    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; } = null;
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

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
}