using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

public partial class ThemeValidationSummary
{
    [Parameter, EditorRequired] public IReadOnlyList<ThemeContrastResult> Results { get; set; } = [];
    [Parameter, EditorRequired] public IReadOnlyList<string> Problems { get; set; } = [];
}
