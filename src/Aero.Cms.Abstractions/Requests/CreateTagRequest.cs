namespace Aero.Cms.Abstractions.Requests;

[GenerateSerializer]
[Alias("CreateTagRequest")]
public record CreateTagRequest(
    long siteId,
    string Name,
    string? Description = null
) : IRequest;

[GenerateSerializer]
[Alias("UpdateTagRequest")]
public record UpdateTagRequest(
    long Id,
    string Name,
    string? Description = null
): IRequest;

[GenerateSerializer]
[Alias("DeleteTagRequest")]
public record DeleteTagRequest(
    long Id
): IRequest;