namespace Aero.Cms.Abstractions.Requests;

[GenerateSerializer]
[Alias("CreateSeriesRequest")]
public record CreateSeriesRequest(
    long siteId,
    string Name,
    string? Slug = null,
    string? Description = null
) : IRequest;

[GenerateSerializer]
[Alias("UpdateSeriesRequest")]
public record UpdateSeriesRequest(
    long Id,
    string Name,
    string? Slug = null,
    string? Description = null
) : IRequest;

[GenerateSerializer]
[Alias("DeleteSeriesRequest")]
public record DeleteSeriesRequest(long Id) : IRequest;
