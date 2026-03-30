namespace Aero.Cms.Modules.Pages.Requests;

public record CreateCategoryRequest(
    long siteId,
    string Name,
    string? Description = null
);

public record UpdateCategoryRequest(
    long Id,
    string Name,
    string? Description = null
);

public record DeleteCategoryRequest(
    long Id
);