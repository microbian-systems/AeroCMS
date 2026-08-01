using Aero.Cms.Abstractions.Models;

namespace Aero.Cms.Abstractions.Requests;

[GenerateSerializer]
[Alias("UpdateSiteStyleProfileRequest")]
public sealed record UpdateSiteStyleProfileRequest(
    [property: Id(0)] long ExpectedRevision,
    [property: Id(1)] decimal SmallScreenBreakpointRem,
    [property: Id(2)] List<SiteStyleColorTokenViewModel> ColorTokens) : IRequest;
