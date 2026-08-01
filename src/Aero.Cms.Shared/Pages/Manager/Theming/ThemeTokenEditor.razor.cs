using System.Globalization;
using Aero.Cms.Abstractions.Theming;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

public partial class ThemeTokenEditor
{
    [Parameter, EditorRequired] public ThemeDefaultMode Mode { get; set; }
    [Parameter, EditorRequired] public ThemeDefaultMode DefaultMode { get; set; }
    [Parameter, EditorRequired] public ThemeColorTokens Colors { get; set; } = new();
    [Parameter, EditorRequired] public ThemeShapeTokens Shape { get; set; } = new();
    [Parameter] public EventCallback<ThemeDefaultMode> ModeChanged { get; set; }
    [Parameter] public EventCallback<ThemeDefaultMode> DefaultModeChanged { get; set; }
    [Parameter] public EventCallback<ThemeColorChange> ColorChanged { get; set; }
    [Parameter] public EventCallback<ThemeShapeChange> ShapeChanged { get; set; }

    private IReadOnlyList<ShapeControl> ShapeControls =>
    [
        new("Radius selector", "RadiusSelectorRem", Shape.RadiusSelectorRem, 2m, .05m),
        new("Radius field", "RadiusFieldRem", Shape.RadiusFieldRem, 2m, .05m),
        new("Radius box", "RadiusBoxRem", Shape.RadiusBoxRem, 3m, .05m),
        new("Selector size", "SizeSelectorRem", Shape.SizeSelectorRem, 2m, .05m),
        new("Field size", "SizeFieldRem", Shape.SizeFieldRem, 2m, .05m),
        new("Border", "BorderRem", Shape.BorderRem, .5m, .01m)
    ];

    private Task SelectLightAsync() => ModeChanged.InvokeAsync(ThemeDefaultMode.Light);
    private Task SelectDarkAsync() => ModeChanged.InvokeAsync(ThemeDefaultMode.Dark);
    private Task ChangeDefaultModeAsync(ChangeEventArgs args)
    {
        return Enum.TryParse<ThemeDefaultMode>(args.Value?.ToString(), out var mode)
            ? DefaultModeChanged.InvokeAsync(mode)
            : Task.CompletedTask;
    }
    private Task ChangeColorAsync(string token, string value) => ColorChanged.InvokeAsync(new(Mode, token, value));
    private Task ChangeShapeAsync(string token, decimal value) => ShapeChanged.InvokeAsync(new(token, value));
    private Task ChangeShapeAsync(string token, ChangeEventArgs args) => decimal.TryParse(args.Value?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? ChangeShapeAsync(token, value) : Task.CompletedTask;
    private Task ChangeDepthAsync(ChangeEventArgs args) => ShapeChanged.InvokeAsync(new("Depth", args.Value is true ? 1m : 0m));
    private Task ChangeNoiseAsync(ChangeEventArgs args) => ShapeChanged.InvokeAsync(new("Noise", args.Value is true ? 1m : 0m));

    private sealed record ShapeControl(string Label, string Token, decimal Value, decimal Maximum, decimal Step);
}
