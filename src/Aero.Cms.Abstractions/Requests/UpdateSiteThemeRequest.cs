namespace Aero.Cms.Abstractions.Requests;

/// <summary>
/// Requests an optimistic update to a site's exact installed-theme selection.
/// </summary>
[GenerateSerializer]
[Alias("UpdateSiteThemeRequest")]
public sealed record UpdateSiteThemeRequest(
    [property: Id(0)] long ExpectedRevision,
    [property: Id(1)] string ThemeId,
    [property: Id(2)] string ThemeVersion) : IRequest;
