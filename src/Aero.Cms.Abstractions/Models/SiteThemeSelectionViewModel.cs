namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a site's exact deployment-installed theme selection.
/// </summary>
[GenerateSerializer]
[Alias("SiteThemeSelectionViewModel")]
public sealed record SiteThemeSelectionViewModel(
    [property: Id(0)] string ThemeId,
    [property: Id(1)] string ThemeVersion,
    [property: Id(2)] long ThemeRevision);
