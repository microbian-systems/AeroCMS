using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Rendering;

namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Describes the values used to create a page draft within a site.
/// </summary>
/// <param name="Title">The page title displayed to readers and editors.</param>
/// <param name="Slug">The URL segment used to identify the page within its parent path.</param>
/// <param name="Summary">Optional summary used by page listings and metadata.</param>
/// <param name="SeoTitle">Optional title used for search-engine metadata.</param>
/// <param name="SeoDescription">Optional description used for search-engine metadata.</param>
/// <param name="PublicationState">The initial publication state.</param>
/// <param name="ParentId">The optional parent page identifier; <see langword="null"/> creates a root page.</param>
/// <param name="ShowInNavMenu">Whether the page is included in navigation menus.</param>
/// <param name="ShowHeaderNavigation">Whether header navigation is rendered for the page.</param>
/// <param name="HideFooter">Whether the page suppresses the site footer.</param>
/// <param name="ShowChatAgent">Whether the page enables the chat agent.</param>
/// <param name="SiteId">The owning site identifier.</param>
/// <param name="DraftContentJson">Optional JSON transport for the living-standard draft content.</param>
/// <param name="DraftCompositionJson">Optional JSON transport for draft composition metadata.</param>
/// <param name="RendererId">The stable registered page-renderer identifier.</param>
/// <param name="DraftSource">The exact optional source text for a source-rendered draft.</param>
/// <param name="IncludeInSearch">Whether the published page is eligible for site search.</param>
/// <param name="IncludeInPublicAi">Whether the published page may ground public AI answers.</param>
[GenerateSerializer]
[Alias("CreatePageRequest")]
public record CreatePageRequest(
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    long? ParentId = null,
    bool ShowInNavMenu = false,
    bool ShowHeaderNavigation = true,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    long SiteId = 0,
    string? DraftContentJson = null,
    string? DraftCompositionJson = null,
    string RendererId = PageRendererIds.AeroComposition,
    string? DraftSource = null,
    bool IncludeInSearch = true,
    bool IncludeInPublicAi = false
) : IRequest;

/// <summary>
/// Describes the values used to update an existing page.
/// </summary>
/// <param name="Id">The identifier of the page to update.</param>
/// <param name="Title">The page title displayed to readers and editors.</param>
/// <param name="Slug">The URL segment to use for the page.</param>
/// <param name="Summary">Optional summary used by page listings and metadata.</param>
/// <param name="SeoTitle">Optional title used for search-engine metadata.</param>
/// <param name="SeoDescription">Optional description used for search-engine metadata.</param>
/// <param name="PublicationState">The publication state to apply.</param>
/// <param name="ParentId">The optional parent page identifier; <see langword="null"/> places the page at the root.</param>
/// <param name="ShowInNavMenu">Whether the page is included in navigation menus.</param>
/// <param name="ShowHeaderNavigation">Whether header navigation is rendered for the page.</param>
/// <param name="HideFooter">Whether the page suppresses the site footer.</param>
/// <param name="ShowChatAgent">Whether the page enables the chat agent.</param>
/// <param name="DraftContentJson">Optional JSON transport for the living-standard draft content.</param>
/// <param name="PreviousPathBehavior">Optional instruction for handling the page's prior route when its path changes.</param>
/// <param name="DraftCompositionJson">Optional JSON transport for draft composition metadata.</param>
/// <param name="RendererId">The stable registered page-renderer identifier.</param>
/// <param name="DraftSource">The exact replacement source text, or <see langword="null"/> to preserve it.</param>
/// <param name="IncludeInSearch">Whether the published page is eligible for site search.</param>
/// <param name="IncludeInPublicAi">Whether the published page may ground public AI answers.</param>
[GenerateSerializer]
[Alias("UpdatePageRequest")]
public record UpdatePageRequest(
    long Id,
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    long? ParentId = null,
    bool ShowInNavMenu = false,
    bool ShowHeaderNavigation = true,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    string? DraftContentJson = null,
    PreviousPathBehavior? PreviousPathBehavior = null,
    string? DraftCompositionJson = null,
    string RendererId = PageRendererIds.AeroComposition,
    string? DraftSource = null,
    bool IncludeInSearch = true,
    bool IncludeInPublicAi = false
) : IRequest;

/// <summary>
/// Represents a record for DeletePageRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeletePageRequest")]
public record DeletePageRequest(
    long Id
) : IRequest;
