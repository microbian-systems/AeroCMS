using Aero.Cms.Core.Blocks.Layout;
using Aero.Cms.Core;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Pages.Requests;

public record CreatePageRequest(
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    IReadOnlyList<LayoutRegion>? LayoutRegions = null,
    bool ShowInNavMenu = false,
    IReadOnlyList<EditorBlock>? EditorBlocks = null
);

public record UpdatePageRequest(
    long Id,
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    IReadOnlyList<LayoutRegion>? LayoutRegions = null,
    bool ShowInNavMenu = false,
    IReadOnlyList<EditorBlock>? EditorBlocks = null
);


public record DeletePageRequest(
    long Id
);