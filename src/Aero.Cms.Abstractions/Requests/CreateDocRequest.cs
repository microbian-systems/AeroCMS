using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Pages.Requests;

public record CreateDocRequest(
    long SiteId,
    string Title,
    string Slug,
    string? Summary = null,
    string? SeoTitle = null,
    string? SeoDescription = null,
    string? Content = null,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    string? Markdown = null
);

public record UpdateDocRequest(
    long Id,
    string Title,
    string Slug,
    string? Summary = null,
    string? SeoTitle = null,
    string? SeoDescription = null,
    string? Content = null,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    string? Markdown = null
);

public record DeleteDocRequest(
    long Id
);