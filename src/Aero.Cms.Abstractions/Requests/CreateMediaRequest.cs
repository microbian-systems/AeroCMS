namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreateMediaRequest.
/// </summary>
[GenerateSerializer]
[Alias("CreateMediaRequest")]
public record CreateMediaRequest(
    string FileName,
    string ContentType,
    byte[] Content,
    string? Description = null,
    string? Title = null,
    string? Thumbnail = null,
    Dictionary<string, string>? Metadata = null
) : IRequest;


/// <summary>
/// Represents a record for UpdateMediaRequest.
/// </summary>
[GenerateSerializer]
[Alias("UpdateMediaRequest")]
public record UpdateMediaRequest(
    long Id,
    long SiteId,
    string? Description = null,
    string? Title = null,
    string? Thumbnail = null,
    Dictionary<string, string>? Metadata = null
);

/// <summary>
/// Represents a record for DeleteMediaRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeleteMediaRequest")]
public record DeleteMediaRequest(
    long Id
);