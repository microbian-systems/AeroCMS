namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreateSiteRequest.
/// </summary>
[GenerateSerializer]
[Alias("CreateSiteRequest")]
public record CreateSiteRequest(
    string? Name,
    string? PrimaryHost,
    List<string>? Hosts = null,
    string? Description = null,
    bool IsDefault = false,
    string? DefaultCulture = null,
    List<string>? SupportedCultures = null
) : IRequest;

/// <summary>
/// Represents a record for UpdateSiteRequest.
/// </summary>
[GenerateSerializer]
[Alias("UpdateSiteRequest")]
public record UpdateSiteRequest(
    long Id,
    string? Name,
    string? PrimaryHost,
    List<string>? Hosts = null,
    string? Description = null,
    bool IsDefault = false,
    string? DefaultCulture = null,
    List<string>? SupportedCultures = null
): IRequest;

/// <summary>
/// Represents a record for DeleteSiteRequest.
/// </summary>
[GenerateSerializer]
[Alias("DelteSiteRequest")]
public record DeleteSiteRequest(
    long Id
): IRequest;
