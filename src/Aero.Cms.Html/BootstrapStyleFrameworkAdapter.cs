namespace Aero.Cms.Html;

/// <summary>
/// Exact mappings for Bootstrap utility classes. Grid-template intent and
/// site-specific responsive breakpoints intentionally fall back to native CSS.
/// </summary>
public sealed class BootstrapStyleFrameworkAdapter : StyleFrameworkAdapterBase
{
    /// <inheritdoc />
    public override string AdapterId => "bootstrap";

    /// <inheritdoc />
    public override string AdapterVersion => "1";

    /// <inheritdoc />
    protected override void MapLayout(HtmlStyle residual, ICollection<string> classes)
    {
        if (residual.StackOnSmallScreens || residual.GridColumns is not null)
            return;

        if (TryAdd(DisplayClass(residual.Display), classes))
            residual.Display = null;
        if (TryAdd(FlexDirectionClass(residual.FlexDirection), classes))
            residual.FlexDirection = null;
        if (TryAdd(LengthClass("gap", residual.Gap), classes))
            residual.Gap = null;
        if (TryAdd(AlignmentClass(residual.AlignItems), classes))
            residual.AlignItems = null;
        if (TryAdd(JustificationClass(residual.JustifyContent), classes))
            residual.JustifyContent = null;
    }

    /// <inheritdoc />
    protected override void MapSpacing(HtmlStyle residual, ICollection<string> classes)
    {
        if (TryMapUniformSpacing(residual.Padding, length => LengthClass("p", length), classes))
            residual.Padding = null;
        if (TryMapUniformSpacing(residual.Margin, length => LengthClass("m", length), classes))
            residual.Margin = null;
    }

    /// <inheritdoc />
    protected override void MapSizing(HtmlStyle residual, ICollection<string> classes)
    {
    }

    /// <inheritdoc />
    protected override void MapSurface(HtmlStyle residual, ICollection<string> classes)
    {
    }

    /// <inheritdoc />
    protected override void MapTypography(HtmlStyle residual, ICollection<string> classes)
    {
        if (residual.Typography is not { } typography)
            return;

        if (TryAdd(TextAlignmentClass(typography.Alignment), classes))
            typography.Alignment = null;

        var weightClass = typography.FontWeight switch
        {
            400 => "fw-normal",
            700 => "fw-bold",
            _ => null
        };
        if (TryAdd(weightClass, classes))
            typography.FontWeight = null;
    }

    /// <summary>Adds a utility class only when an exact mapping was found.</summary>
    private static bool TryAdd(string? className, ICollection<string> classes)
    {
        if (className is null)
            return false;
        classes.Add(className);
        return true;
    }

    /// <summary>Maps the small built-in Bootstrap spacing scale without rounding arbitrary values.</summary>
    private static string? LengthClass(string prefix, CssLength? length)
    {
        if (length?.Unit is not CssLengthUnit.Rem)
            return null;
        var scale = length.Value switch
        {
            0m => "0",
            0.25m => "1",
            0.5m => "2",
            1m => "3",
            1.5m => "4",
            3m => "5",
            _ => null
        };
        return scale is null ? null : $"{prefix}-{scale}";
    }

    /// <summary>Resolves an exact Bootstrap display utility.</summary>
    private static string? DisplayClass(CssDisplay? value) => value switch
    {
        CssDisplay.Block => "d-block",
        CssDisplay.Inline => "d-inline",
        CssDisplay.InlineBlock => "d-inline-block",
        CssDisplay.Flex => "d-flex",
        CssDisplay.InlineFlex => "d-inline-flex",
        CssDisplay.Grid => "d-grid",
        CssDisplay.None => "d-none",
        _ => null
    };

    /// <summary>Resolves an exact Bootstrap flex-direction utility.</summary>
    private static string? FlexDirectionClass(CssFlexDirection? value) => value switch
    {
        CssFlexDirection.Row => "flex-row",
        CssFlexDirection.RowReverse => "flex-row-reverse",
        CssFlexDirection.Column => "flex-column",
        CssFlexDirection.ColumnReverse => "flex-column-reverse",
        _ => null
    };

    /// <summary>Resolves an exact Bootstrap cross-axis alignment utility.</summary>
    private static string? AlignmentClass(CssAlignment? value) => value switch
    {
        CssAlignment.Start => "align-items-start",
        CssAlignment.Center => "align-items-center",
        CssAlignment.End => "align-items-end",
        CssAlignment.Stretch => "align-items-stretch",
        CssAlignment.Baseline => "align-items-baseline",
        _ => null
    };

    /// <summary>Resolves an exact Bootstrap main-axis distribution utility.</summary>
    private static string? JustificationClass(CssJustification? value) => value switch
    {
        CssJustification.Start => "justify-content-start",
        CssJustification.Center => "justify-content-center",
        CssJustification.End => "justify-content-end",
        CssJustification.SpaceBetween => "justify-content-between",
        CssJustification.SpaceAround => "justify-content-around",
        CssJustification.SpaceEvenly => "justify-content-evenly",
        _ => null
    };

    /// <summary>Resolves an exact Bootstrap text-alignment utility.</summary>
    private static string? TextAlignmentClass(CssTextAlignment? value) => value switch
    {
        CssTextAlignment.Start => "text-start",
        CssTextAlignment.Center => "text-center",
        CssTextAlignment.End => "text-end",
        _ => null
    };
}
