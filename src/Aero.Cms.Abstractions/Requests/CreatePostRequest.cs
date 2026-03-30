using Aero.Cms.Core.Blocks;
using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Pages.Requests;

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
);

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
);

public record DeletePostRequest(
    long Id
);
