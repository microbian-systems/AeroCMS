using Aero.Cms.Abstractions.Theming;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

public partial class ThemeHistoryPanel
{
    [Parameter] public IReadOnlyList<ThemeVersionView> Versions { get; set; } = [];
    [Parameter] public IReadOnlyList<SiteThemePublicationView> Publications { get; set; } = [];
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<ThemeAssignmentRequest> Assign { get; set; }

    private Task AssignVersionAsync(ThemeVersionView version) => Assign.InvokeAsync(new(version.ThemeId, version.Version));
    private Task RestorePreviousAsync(SiteThemePublicationView publication) => Assign.InvokeAsync(new(publication.PreviousThemeId!, publication.PreviousVersion!));
    private static string ShortHash(string value) => value[..Math.Min(8, value.Length)];
}
