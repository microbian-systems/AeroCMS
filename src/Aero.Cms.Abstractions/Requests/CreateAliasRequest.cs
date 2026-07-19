namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Represents a record for CreateAliasRequest.
/// </summary>
[GenerateSerializer]
[Alias("CreateAliasRequest")]
public record CreateAliasRequest(
    long SiteId,
    string OldPath,
    string NewPath,
    string? Notes = null,
    string Culture = "en-US"
) : IRequest;

/// <summary>
/// Represents a record for UpdateAliasRequest.
/// </summary>
[GenerateSerializer]
[Alias("UpdateAliasRequest")]
public record UpdateAliasRequest(
    long Id,
    string OldPath,
    string NewPath,
    string? Notes = null,
    string Culture = "en-US"
) : IRequest;

/// <summary>
/// Represents a record for DeleteAliasRequest.
/// </summary>
[GenerateSerializer]
[Alias("DeleteAliasRequest")]
public record DeleteAliasRequest(
    long Id
) : IRequest;
