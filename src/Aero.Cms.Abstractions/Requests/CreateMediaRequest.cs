namespace Aero.Cms.Modules.Pages.Requests;

public record CreateMediaRequest(
    string FileName,
    string ContentType,
    byte[] Content,
    string? Description = null,
    string? Title = null,
    string? Thumbnail = null,
    Dictionary<string, string>? Metadata = null
);


public record UpdateMediaRequest(
    long Id,
    long SiteId,
    string? Description = null,
    string? Title = null,
    string? Thumbnail = null,
    Dictionary<string, string>? Metadata = null
);

public record DeleteMediaRequest(
    long Id
);