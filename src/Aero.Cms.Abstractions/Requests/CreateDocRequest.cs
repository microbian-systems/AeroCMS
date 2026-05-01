using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Requests;

[GenerateSerializer]
[Alias("CreateDocRequest")]
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
) : IRequest;


[GenerateSerializer]
[Alias("UpdateDocRequest")]
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
) : IRequest;


[GenerateSerializer]
[Alias("DeleteDocRequest")]
public record DeleteDocRequest(
    long Id
) : IRequest;