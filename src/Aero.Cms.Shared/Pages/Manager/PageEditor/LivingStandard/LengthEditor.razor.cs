using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Edits an optional CSS length as a numeric value paired with an allowlisted unit.
/// </summary>
public partial class LengthEditor
{
    /// <summary>
    /// Gets or sets the field label rendered for the length control.
    /// </summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the mutable value shared with the owning inspector form.
    /// </summary>
    /// <remarks>
    /// Change handlers update this instance before invoking <see cref="ValueChanged"/>.
    /// </remarks>
    [Parameter, EditorRequired]
    public HtmlElementPropertyPanel.LengthField Value { get; set; } = new();

    /// <summary>
    /// Gets or sets the callback invoked after either the numeric value or unit changes.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlElementPropertyPanel.LengthField> ValueChanged { get; set; }

    /// <summary>
    /// Applies a numeric change to the shared field and notifies the owner.
    /// </summary>
    /// <param name="value">
    /// The new numeric value, or <see langword="null"/> to represent an unspecified length.
    /// </param>
    /// <returns>A task that completes when the change callback has finished.</returns>
    protected async Task OnValueChanged(decimal? value)
    {
        Value.Value = value;
        await ValueChanged.InvokeAsync(Value);
    }

    /// <summary>
    /// Applies a CSS unit change to the shared field and notifies the owner.
    /// </summary>
    /// <param name="unit">The allowlisted CSS length unit selected by the user.</param>
    /// <returns>A task that completes when the change callback has finished.</returns>
    protected async Task OnUnitChanged(CssLengthUnit unit)
    {
        Value.Unit = unit;
        await ValueChanged.InvokeAsync(Value);
    }

    /// <summary>
    /// Converts a CSS length unit to the token displayed beside the numeric input.
    /// </summary>
    /// <param name="unit">The unit to label.</param>
    /// <returns>The CSS unit token, or the enum name for an unrecognized value.</returns>
    protected static string UnitLabel(CssLengthUnit unit) => unit switch
    {
        CssLengthUnit.Pixel => "px",
        CssLengthUnit.Rem => "rem",
        CssLengthUnit.Em => "em",
        CssLengthUnit.Percent => "%",
        CssLengthUnit.ViewportHeight => "vh",
        CssLengthUnit.ViewportWidth => "vw",
        _ => unit.ToString()
    };
}
