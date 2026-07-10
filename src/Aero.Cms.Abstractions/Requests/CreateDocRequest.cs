using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreateDocRequest.
/// </summary>
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


/// <summary>
/// Represents a record for UpdateDocRequest.
/// </summary>
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


/// <summary>
/// Represents a record for DeleteDocRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeleteDocRequest")]
public record DeleteDocRequest(
    long Id
) : IRequest;