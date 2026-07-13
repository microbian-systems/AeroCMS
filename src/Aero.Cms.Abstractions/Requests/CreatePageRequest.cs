using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreatePageRequest.
/// </summary>
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
    IReadOnlyList<LayoutRegion>? LayoutRegions = null,
    bool ShowInNavMenu = false,
    bool ShowHeaderNavigation = true,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    long SiteId = 0,
    /// <summary>JSON-serialized LayoutRegions for Orleans-safe grain transport.</summary>
    string? LayoutRegionsJson = null,
    /// <summary>JSON-serialized NeoPageNode root tree.</summary>
    string? RootNodeJson = null,
    /// <summary>Source-generated JSON transport for living-standard draft content.</summary>
    string? DraftContentJson = null
) : IRequest;

/// <summary>
/// Represents a record for UpdatePageRequest.
/// </summary>
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
    IReadOnlyList<LayoutRegion>? LayoutRegions = null,
    bool ShowInNavMenu = false,
    bool ShowHeaderNavigation = true,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    /// <summary>JSON-serialized LayoutRegions for Orleans-safe grain transport.</summary>
    string? LayoutRegionsJson = null,
    /// <summary>JSON-serialized NeoPageNode root tree.</summary>
    string? RootNodeJson = null,
    /// <summary>Source-generated JSON transport for living-standard draft content.</summary>
    string? DraftContentJson = null
) : IRequest;

/// <summary>
/// Represents a record for DeletePageRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeletePageRequest")]
public record DeletePageRequest(
    long Id
) : IRequest;
