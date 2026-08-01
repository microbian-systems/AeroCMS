namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreateCategoryRequest.
/// </summary>
[GenerateSerializer]
[Alias("CreateCategoryRequest")]
public record CreateCategoryRequest(
    long siteId,
    string Name,
    string? Slug = null,
    string? Description = null
) : IRequest;

/// <summary>
/// Represents a record for UpdateCategoryRequest.
/// </summary>
[GenerateSerializer]
[Alias("UpdateCategoryRequest")]
public record UpdateCategoryRequest(
    long Id,
    string Name,
    string? Slug = null,
    string? Description = null
) : IRequest;


/// <summary>
/// Represents a record for DeleteCategoryRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeleteCategoryRequest")]
public record DeleteCategoryRequest(
    long Id
) : IRequest;