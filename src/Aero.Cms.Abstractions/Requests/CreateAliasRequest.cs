using Aero.Core.Requests;

namespace Aero.Cms.Modules.Pages.Requests;

[GenerateSerializer]
[Alias("CreateAliasRequest")]
public record CreateAliasRequest(
    long SiteId,
    string OldPath,
    string NewPath,
    string? Notes = null
) : IRequest;

[GenerateSerializer]
[Alias("UpdateAliasRequest")]
public record UpdateAliasRequest(
    long Id,
    string OldPath,
    string NewPath,
    string? Notes = null
) : IRequest;

[GenerateSerializer]
[Alias("DeleteAliasRequest")]
public record DeleteAliasRequest(
    long Id
) : IRequest;