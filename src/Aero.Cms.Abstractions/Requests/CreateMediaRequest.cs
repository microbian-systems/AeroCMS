namespace Aero.Cms.Modules.Pages.Requests;

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

[GenerateSerializer]
[Alias("DeleteMediaRequest")]
public record DeleteMediaRequest(
    long Id
);