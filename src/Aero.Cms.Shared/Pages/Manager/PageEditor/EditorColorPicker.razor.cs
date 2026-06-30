using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

public partial class EditorColorPicker
{
    private readonly string PickerId = $"pe-color-{Guid.NewGuid():N}";

    [Parameter] public string? Label { get; set; }

    [Parameter] public CssColor? Value { get; set; }

    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    private string ColorValue => Value?.Value ?? "rgba(0, 0, 0, 1.00)";

    private Task OnColorChanged(string? value) =>
        ValueChanged.InvokeAsync(value);

    private Task ClearColor() =>
        ValueChanged.InvokeAsync(null);
}
