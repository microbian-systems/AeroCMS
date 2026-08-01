using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

public partial class ThemeColorPairEditor
{
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Background { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Foreground { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> BackgroundChanged { get; set; }
    [Parameter] public EventCallback<string> ForegroundChanged { get; set; }

    private Task OnBackgroundPickerChanged(string? value) => BackgroundChanged.InvokeAsync(value ?? string.Empty);
    private Task OnForegroundPickerChanged(string? value) => ForegroundChanged.InvokeAsync(value ?? string.Empty);
    private Task OnBackgroundTextChanged(ChangeEventArgs args) => BackgroundChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);
    private Task OnForegroundTextChanged(ChangeEventArgs args) => ForegroundChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);
}
