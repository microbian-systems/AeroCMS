using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class LengthEditor
{
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public HtmlElementPropertyPanel.LengthField Value { get; set; } = new();

    [Parameter]
    public EventCallback<HtmlElementPropertyPanel.LengthField> ValueChanged { get; set; }

    protected async Task OnValueChanged(decimal? value)
    {
        Value.Value = value;
        await ValueChanged.InvokeAsync(Value);
    }

    protected async Task OnUnitChanged(CssLengthUnit unit)
    {
        Value.Unit = unit;
        await ValueChanged.InvokeAsync(Value);
    }

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
