namespace Aero.Cms.Abstractions.Requests;

[GenerateSerializer]
[Alias("CreateSiteRequest")]
public record CreateSiteRequest(
    string? Name,
    string? Hostname,
    string? Description = null,
    bool IsDefault = false
) : IRequest;

[GenerateSerializer]
[Alias("UpdateSiteRequest")]
public record UpdateSiteRequest(
    long Id,
    string? Name,
    string? Hostname,
    string? Description = null,
    bool IsDefault = false
): IRequest;

[GenerateSerializer]
[Alias("DelteSiteRequest")]
public record DeleteSiteRequest(
    long Id
): IRequest;