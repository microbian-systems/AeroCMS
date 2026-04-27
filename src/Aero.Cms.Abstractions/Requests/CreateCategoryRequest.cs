namespace Aero.Cms.Abstractions.Requests;

[GenerateSerializer]
[Alias("CreateCategoryRequest")]
public record CreateCategoryRequest(
    long siteId,
    string Name,
    string? Description = null
) : IRequest;

[GenerateSerializer]
[Alias("UpdateCategoryRequest")]
public record UpdateCategoryRequest(
    long Id,
    string Name,
    string? Description = null
) : IRequest;


[GenerateSerializer]
[Alias("DeleteCategoryRequest")]
public record DeleteCategoryRequest(
    long Id
) : IRequest;