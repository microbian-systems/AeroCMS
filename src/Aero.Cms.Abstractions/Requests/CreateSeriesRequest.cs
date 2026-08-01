namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreateSeriesRequest.
/// </summary>
[GenerateSerializer]
[Alias("CreateSeriesRequest")]
public record CreateSeriesRequest(
    long siteId,
    string Name,
    string? Slug = null,
    string? Description = null
) : IRequest;

/// <summary>
/// Represents a record for UpdateSeriesRequest.
/// </summary>
[GenerateSerializer]
[Alias("UpdateSeriesRequest")]
public record UpdateSeriesRequest(
    long Id,
    string Name,
    string? Slug = null,
    string? Description = null
) : IRequest;

/// <summary>
/// Represents a record for DeleteSeriesRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeleteSeriesRequest")]
public record DeleteSeriesRequest(long Id) : IRequest;
