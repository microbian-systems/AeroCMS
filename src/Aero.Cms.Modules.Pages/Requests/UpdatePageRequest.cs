namespace Aero.Cms.Modules.Pages.Requests;

public record UpdatePageRequest(
    long Id,
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    ContentPublicationState PublicationState = ContentPublicationState.Draft
);