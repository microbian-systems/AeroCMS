using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreatePostRequest.
/// </summary>
[GenerateSerializer]
[Alias("CreatePostRequest")]
public record CreatePostRequest(
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    string? AuthorName,
    DateTimeOffset? PublishDate,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    IReadOnlyList<EditorBlock>? EditorBlocks = null,
    long SiteId = 0
): IRequest;

/// <summary>
/// Represents a record for UpdatePostRequest.
/// </summary>
[GenerateSerializer]
[Alias("UpdatePostRequest")]
public record UpdatePostRequest(
    long Id,
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    string? AuthorName,
    DateTimeOffset? PublishDate,
    ContentPublicationState PublicationState = ContentPublicationState.Draft,
    IReadOnlyList<EditorBlock>? EditorBlocks = null
): IRequest;

/// <summary>
/// Represents a record for DeletePostRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeletePostRequest")]
public record DeletePostRequest(
    long Id
): IRequest;
