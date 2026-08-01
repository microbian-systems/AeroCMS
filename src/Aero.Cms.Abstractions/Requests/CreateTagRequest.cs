namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreateTagRequest.
/// </summary>
[GenerateSerializer]
[Alias("CreateTagRequest")]
public record CreateTagRequest(
    long siteId,
    string Name,
    string? Slug = null,
    string? Description = null
) : IRequest;

/// <summary>
/// Represents a record for UpdateTagRequest.
/// </summary>
[GenerateSerializer]
[Alias("UpdateTagRequest")]
public record UpdateTagRequest(
    long Id,
    string Name,
    string? Slug = null,
    string? Description = null
): IRequest;

/// <summary>
/// Represents a record for DeleteTagRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeleteTagRequest")]
public record DeleteTagRequest(
    long Id
): IRequest;