namespace Aero.Cms.Modules.Pages.Requests;

public record CreateTagRequest(
    long siteId,
    string Name,
    string? Description = null
);

public record UpdateTagRequest(
    long Id,
    string Name,
    string? Description = null
);

public record DeleteTagRequest(
    long Id
);