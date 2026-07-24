using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Rendering;

namespace Aero.Cms.Abstractions.Models;


/// <summary>
/// Represents a record for PageViewModel.
/// </summary>
[Alias("PageViewModel")]
[GenerateSerializer]
public record PageViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
[Id(0)]
    public string? Title { get; init; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[Id(1)]
    public string? Slug { get; init; } 
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
[Id(2)]
    public PageKind Kind { get; init; }
        /// <summary>
    /// Gets or sets the Content.
    /// </summary>
[Id(3)]
    public string? Content { get; init; }
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
[Id(4)]
    public string? Author { get; init; }
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
[Id(5)]
    public IReadOnlyList<string> Tags { get; init; } = [];
        /// <summary>
    /// Gets or sets the Categories.
    /// </summary>
[Id(6)]
    public IReadOnlyList<string> Categories { get; init; } = [];
        /// <summary>
    /// Gets or sets the Blocks.
    /// </summary>
[Id(7)]
    public IReadOnlyList<object> Blocks { get; init; } = [];
        /// <summary>
    /// Gets or sets the Is Published.
    /// </summary>
[Id(8)]
    public bool IsPublished { get; init; }
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
[Id(9)]
    public DateTimeOffset? PublishedOn { get; init; }
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
[Id(10)]
    public long SiteId { get; init; }
        /// <summary>
    /// Gets or sets the Parent Id.
    /// </summary>
[Id(11)]
    public long? ParentId { get; init; }
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
[Id(12)]
    public string? Path { get; init; }
        /// <summary>
    /// Gets or sets the Depth.
    /// </summary>
[Id(13)]
    public int Depth { get; init; }
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
[Id(14)]
    public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Is Hidden.
    /// </summary>
[Id(15)]
    public bool IsHidden { get; init; }
        /// <summary>
    /// Gets or sets the Show In Nav Menu.
    /// </summary>
[Id(16)]
    public bool ShowInNavMenu { get; init; } = true;
        /// <summary>
    /// Gets or sets the Summary.
    /// </summary>
[Id(17)]
    public string? Summary { get; init; }
        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
[Id(18)]
    public string? SeoTitle { get; init; }
        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
[Id(19)]
    public string? SeoDescription { get; init; }
        /// <summary>
    /// Gets or sets the Show Header Navigation.
    /// </summary>
[Id(20)]
    public bool ShowHeaderNavigation { get; init; } = true;
        /// <summary>
    /// Gets or sets the Hide Footer.
    /// </summary>
[Id(21)]
    public bool HideFooter { get; init; }
        /// <summary>
    /// Gets or sets the Show Chat Agent.
    /// </summary>
[Id(22)]
    public bool ShowChatAgent { get; init; } = true;
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
[Id(24)]
    public string Culture { get; init; } = "en-US";
        /// <summary>
    /// Gets or sets the Translation Group Id.
    /// </summary>
[Id(25)]
    public long? TranslationGroupId { get; init; }
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
[Id(26)]
    public ContentPublicationState PublicationState { get; init; } = ContentPublicationState.Draft;
        /// <summary>
    /// Gets or sets the Content Revision.
    /// </summary>
[Id(30)]
    public long ContentRevision { get; init; }
        /// <summary>
    /// Gets the source-generated JSON transport for living-standard draft content.
    /// </summary>
[Id(31)]
    public string? DraftContentJson { get; init; }
        /// <summary>
    /// Gets the source-generated JSON transport for the published content snapshot.
    /// </summary>
    [Id(32)]
    public string? PublishedContentJson { get; init; }

    /// <summary>
    /// Gets the JSON transport for draft page-composition metadata.
    /// </summary>
    [Id(33)]
    public string? DraftCompositionJson { get; init; }

    /// <summary>
    /// Gets the JSON transport for the published page-composition snapshot.
    /// </summary>
    [Id(34)]
    public string? PublishedCompositionJson { get; init; }

    /// <summary>Gets the stable rendering strategy identifier selected for this page.</summary>
    [Id(35)]
    public string RendererId { get; init; } = PageRendererIds.AeroComposition;

    /// <summary>Gets whether the editable draft differs from the last published snapshot.</summary>
    [Id(36)]
    public bool HasUnpublishedChanges { get; init; }
}

/// <summary>
/// Represents a record for PageErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("PageErrorViewModel")]
public record PageErrorViewModel : AeroErrorViewModel<PageViewModel>;
