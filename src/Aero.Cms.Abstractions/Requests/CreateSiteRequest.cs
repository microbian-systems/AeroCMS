namespace Aero.Cms.Modules.Pages.Requests;

public record CreateSiteRequest(
    string? Name,
    string? Hostname,
    string? Description = null,
    bool IsDefault = false
);

public record UpdateSiteRequest(
    long Id,
    string? Name,
    string? Hostname,
    string? Description = null,
    bool IsDefault = false
);

public record DeleteSiteRequest(
    long Id
);