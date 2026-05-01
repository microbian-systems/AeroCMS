using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Requests;

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
    IReadOnlyList<EditorBlock>? EditorBlocks = null
): IRequest;

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

[GenerateSerializer]
[Alias("DeletePostRequest")]
public record DeletePostRequest(
    long Id
): IRequest;
