using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

public partial class ThemePreviewGallery
{
    [Parameter, EditorRequired] public string DataThemeName { get; set; } = "theme-studio-light";
    [Parameter] public ThemeStudioPanel Panel { get; set; }
    [Parameter] public ThemeStudioViewport Viewport { get; set; } = ThemeStudioViewport.Desktop;
    [Parameter] public EventCallback<ThemeStudioPanel> PanelChanged { get; set; }
    [Parameter] public EventCallback<ThemeStudioViewport> ViewportChanged { get; set; }

    private static IReadOnlyList<ViewportOption> Viewports { get; } =
    [
        new("Phone", "rzi-smartphone", ThemeStudioViewport.Phone),
        new("Tablet", "rzi-tablet", ThemeStudioViewport.Tablet),
        new("Desktop", "rzi-desktop-windows", ThemeStudioViewport.Desktop)
    ];

    private static IReadOnlyList<FeatureCard> FeatureCards { get; } =
    [
        new("01", "Compose", "Build pages from reusable visual primitives.", "d-badge-primary"),
        new("02", "Validate", "See accessibility feedback before publishing.", "d-badge-secondary"),
        new("03", "Release", "Assign an immutable theme version to the site.", "d-badge-accent")
    ];

    private Task ShowComponentsAsync() => PanelChanged.InvokeAsync(ThemeStudioPanel.Components);
    private Task ShowPatternsAsync() => PanelChanged.InvokeAsync(ThemeStudioPanel.Patterns);
    private Task ChangeViewportAsync(ThemeStudioViewport viewport) => ViewportChanged.InvokeAsync(viewport);

    private sealed record ViewportOption(string Label, string Icon, ThemeStudioViewport Value);
    private sealed record FeatureCard(string Kicker, string Title, string Copy, string BadgeClass);
}
