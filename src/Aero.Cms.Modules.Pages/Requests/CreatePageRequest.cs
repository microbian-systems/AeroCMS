using Aero.Cms.Core;

namespace Aero.Cms.Modules.Pages.Requests;

public record CreatePageRequest(
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    ContentPublicationState PublicationState = ContentPublicationState.Draft
);