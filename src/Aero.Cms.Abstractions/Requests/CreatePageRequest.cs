using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Requests;

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
    IReadOnlyList<EditorBlock>? EditorBlocks = null,
    long SiteId = 0,
    /// <summary>JSON-serialized EditorBlocks for Orleans-safe grain transport.</summary>
    string? EditorBlocksJson = null,
    /// <summary>JSON-serialized LayoutRegions for Orleans-safe grain transport.</summary>
    string? LayoutRegionsJson = null
) : IRequest;

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
    IReadOnlyList<EditorBlock>? EditorBlocks = null,
    /// <summary>JSON-serialized EditorBlocks for Orleans-safe grain transport.
    /// null = omitted (preserve existing); non-null = apply (empty string = clear blocks).</summary>
    string? EditorBlocksJson = null,
    /// <summary>JSON-serialized LayoutRegions for Orleans-safe grain transport.</summary>
    string? LayoutRegionsJson = null
) : IRequest;

[GenerateSerializer]
[Alias("DeletePageRequest")]
public record DeletePageRequest(
    long Id
) : IRequest;
