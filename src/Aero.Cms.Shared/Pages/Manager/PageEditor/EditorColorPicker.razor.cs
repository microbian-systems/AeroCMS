using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// Represents a class for EditorColorPicker.
/// </summary>
public partial class EditorColorPicker
{
    private readonly string PickerId = $"pe-color-{Guid.NewGuid():N}";

        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
[Parameter] public string? Label { get; set; }

        /// <summary>
    /// Gets or sets the Value.
    /// </summary>
[Parameter] public CssColor? Value { get; set; }

        /// <summary>
    /// Gets or sets the Value Changed.
    /// </summary>
[Parameter] public EventCallback<string?> ValueChanged { get; set; }

    private string ColorValue => Value?.Value ?? "rgba(0, 0, 0, 1.00)";

    private Task OnColorChanged(string? value) =>
        ValueChanged.InvokeAsync(value);

    private Task ClearColor() =>
        ValueChanged.InvokeAsync(null);
}
