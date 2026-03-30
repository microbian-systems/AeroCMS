namespace Aero.Cms.Modules.Pages.Requests;

public record CreateAliasRequest(
    long SiteId,
    string OldPath,
    string NewPath,
    string? Notes = null
);

public record UpdateAliasRequest(
    long Id,
    string OldPath,
    string NewPath,
    string? Notes = null
);

public record DeleteAliasRequest(
    long Id
);