using System.Globalization;

namespace Aero.Cms.Html;

/// <summary>
/// Exact mappings for a stable Tailwind utility subset. The consuming theme must
/// include or safelist these documented classes; arbitrary-value classes are never emitted.
/// </summary>
public sealed class TailwindStyleFrameworkAdapter : StyleFrameworkAdapterBase
{
    public override string AdapterId => "tailwind";
    public override string AdapterVersion => "1";

    protected override void MapLayout(HtmlStyle residual, ICollection<string> classes)
    {
        // The site breakpoint is not assumed to equal Tailwind's configured breakpoints.
        // Preserve the complete responsive layout group for native fallback.
        if (residual.StackOnSmallScreens)
        {
            return;
        }

        if (Map(residual.Display, DisplayClass, classes))
            residual.Display = null;
        if (Map(residual.FlexDirection, FlexDirectionClass, classes))
            residual.FlexDirection = null;
        if (residual.GridColumns is >= 1 and <= 12)
        {
            classes.Add($"grid-cols-{residual.GridColumns.Value.ToString(CultureInfo.InvariantCulture)}");
            residual.GridColumns = null;
        }
        if (MapLength(residual.Gap, "gap", classes))
            residual.Gap = null;
        if (Map(residual.AlignItems, AlignmentClass, classes))
            residual.AlignItems = null;
        if (Map(residual.JustifyContent, JustificationClass, classes))
            residual.JustifyContent = null;
    }

    protected override void MapSpacing(HtmlStyle residual, ICollection<string> classes)
    {
        if (TryMapUniformSpacing(residual.Padding, length => LengthClass("p", length), classes))
            residual.Padding = null;
        if (TryMapUniformSpacing(residual.Margin, length => LengthClass("m", length), classes))
            residual.Margin = null;
    }

    protected override void MapSizing(HtmlStyle residual, ICollection<string> classes)
    {
        if (residual.MinimumHeight is { Unit: CssLengthUnit.ViewportHeight, Value: 100 })
        {
            classes.Add("min-h-screen");
            residual.MinimumHeight = null;
        }
    }

    protected override void MapSurface(HtmlStyle residual, ICollection<string> classes)
    {
        if (residual.Surface?.BorderRadius is not { } radius)
            return;

        var className = radius is { Unit: CssLengthUnit.Rem, Value: 0 } ? "rounded-none"
            : radius is { Unit: CssLengthUnit.Rem, Value: 0.25m } ? "rounded"
            : radius is { Unit: CssLengthUnit.Rem, Value: 0.5m } ? "rounded-lg"
            : radius is { Unit: CssLengthUnit.Rem, Value: 0.75m } ? "rounded-xl"
            : radius is { Unit: CssLengthUnit.Rem, Value: 1m } ? "rounded-2xl"
            : null;
        if (className is null)
            return;

        classes.Add(className);
        residual.Surface.BorderRadius = null;
    }

    protected override void MapTypography(HtmlStyle residual, ICollection<string> classes)
    {
        if (residual.Typography is not { } typography)
            return;

        if (Map(typography.Alignment, TextAlignmentClass, classes))
            typography.Alignment = null;

        if (typography.FontWeight is >= 100 and <= 900
            && typography.FontWeight % 100 == 0)
        {
            classes.Add($"font-{WeightName(typography.FontWeight.Value)}");
            typography.FontWeight = null;
        }
    }

    private static bool MapLength(CssLength? length, string prefix, ICollection<string> classes)
    {
        if (length is null)
            return false;
        var className = LengthClass(prefix, length);
        if (className is null)
            return false;
        classes.Add(className);
        return true;
    }

    private static string? LengthClass(string prefix, CssLength length)
    {
        if (length.Unit is not CssLengthUnit.Rem)
            return null;

        var scale = length.Value switch
        {
            0m => "0",
            0.25m => "1",
            0.5m => "2",
            0.75m => "3",
            1m => "4",
            1.25m => "5",
            1.5m => "6",
            2m => "8",
            2.5m => "10",
            3m => "12",
            4m => "16",
            _ => null
        };
        return scale is null ? null : $"{prefix}-{scale}";
    }

    private static bool Map<T>(
        T? value,
        Func<T, string?> mapper,
        ICollection<string> classes)
        where T : struct
    {
        if (value is not { } actual)
            return false;
        var className = mapper(actual);
        if (className is null)
            return false;
        classes.Add(className);
        return true;
    }

    private static string? DisplayClass(CssDisplay value) => value switch
    {
        CssDisplay.Block => "block",
        CssDisplay.Inline => "inline",
        CssDisplay.InlineBlock => "inline-block",
        CssDisplay.Flex => "flex",
        CssDisplay.InlineFlex => "inline-flex",
        CssDisplay.Grid => "grid",
        CssDisplay.InlineGrid => "inline-grid",
        CssDisplay.None => "hidden",
        _ => null
    };

    private static string? FlexDirectionClass(CssFlexDirection value) => value switch
    {
        CssFlexDirection.Row => "flex-row",
        CssFlexDirection.RowReverse => "flex-row-reverse",
        CssFlexDirection.Column => "flex-col",
        CssFlexDirection.ColumnReverse => "flex-col-reverse",
        _ => null
    };

    private static string? AlignmentClass(CssAlignment value) => value switch
    {
        CssAlignment.Start => "items-start",
        CssAlignment.Center => "items-center",
        CssAlignment.End => "items-end",
        CssAlignment.Stretch => "items-stretch",
        CssAlignment.Baseline => "items-baseline",
        _ => null
    };

    private static string? JustificationClass(CssJustification value) => value switch
    {
        CssJustification.Start => "justify-start",
        CssJustification.Center => "justify-center",
        CssJustification.End => "justify-end",
        CssJustification.SpaceBetween => "justify-between",
        CssJustification.SpaceAround => "justify-around",
        CssJustification.SpaceEvenly => "justify-evenly",
        _ => null
    };

    private static string? TextAlignmentClass(CssTextAlignment value) => value switch
    {
        CssTextAlignment.Start => "text-start",
        CssTextAlignment.Center => "text-center",
        CssTextAlignment.End => "text-end",
        CssTextAlignment.Justify => "text-justify",
        _ => null
    };

    private static string WeightName(int weight) => weight switch
    {
        100 => "thin",
        200 => "extralight",
        300 => "light",
        400 => "normal",
        500 => "medium",
        600 => "semibold",
        700 => "bold",
        800 => "extrabold",
        900 => "black",
        _ => throw new ArgumentOutOfRangeException(nameof(weight), weight, null)
    };
}
