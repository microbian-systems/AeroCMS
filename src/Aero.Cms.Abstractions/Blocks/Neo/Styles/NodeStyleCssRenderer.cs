using System.Globalization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Renders typed node styles through a fixed CSS property whitelist.
/// </summary>
public static class NodeStyleCssRenderer
{
    public static string Render(
        ResponsiveNodeStyle style,
        EditorBreakpoint breakpoint = EditorBreakpoint.Desktop)
    {
        ArgumentNullException.ThrowIfNull(style);
        return Render(style.Resolve(breakpoint));
    }

    public static string Render(NodeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var declarations = new List<string>();
        AddSpacing(declarations, "margin", style.Margin);
        AddSpacing(declarations, "padding", style.Padding);
        AddLength(declarations, "width", style.Width);
        AddLength(declarations, "height", style.Height);
        AddLength(declarations, "min-width", style.MinimumWidth);
        AddLength(declarations, "max-width", style.MaximumWidth);
        AddLength(declarations, "min-height", style.MinimumHeight);
        AddLength(declarations, "max-height", style.MaximumHeight);
        AddColor(declarations, "color", style.ForegroundColor);
        AddColor(declarations, "background-color", style.BackgroundColor);
        AddBackgroundLayers(
            declarations,
            style.BackgroundGradient,
            style.BackgroundImage,
            style.Direction);
        AddColor(declarations, "border-color", style.BorderColor);
        AddLength(declarations, "border-width", style.BorderWidth);
        AddLength(declarations, "border-radius", style.BorderRadius);
        AddShadows(declarations, style.Shadow, style.BackgroundOverlayColor);
        AddLength(declarations, "font-size", style.FontSize);
        AddLength(declarations, "letter-spacing", style.LetterSpacing);

        if (style.BorderWidth is { } borderWidth && IsValid(borderWidth))
        {
            declarations.Add("border-style:solid");
        }

        if (style.Opacity is >= 0m and <= 1m)
        {
            declarations.Add(
                $"opacity:{style.Opacity.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (style.FontWeight != FontWeight.Inherit &&
            Enum.IsDefined(style.FontWeight))
        {
            declarations.Add($"font-weight:{(int)style.FontWeight}");
        }

        if (style.LineHeight is >= 0.5m and <= 5m)
        {
            declarations.Add(
                $"line-height:{style.LineHeight.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        switch (style.TextAlignment)
        {
            case TextAlignment.Start:
                declarations.Add("text-align:start");
                break;
            case TextAlignment.Center:
                declarations.Add("text-align:center");
                break;
            case TextAlignment.End:
                declarations.Add("text-align:end");
                break;
            case TextAlignment.Justify:
                declarations.Add("text-align:justify");
                break;
        }

        switch (style.Direction)
        {
            case ContentDirection.LeftToRight:
                declarations.Add("direction:ltr");
                break;
            case ContentDirection.RightToLeft:
                declarations.Add("direction:rtl");
                break;
        }

        if (style.Hidden)
        {
            declarations.Add("display:none");
        }

        return string.Join(';', declarations);
    }

    private static void AddColor(
        ICollection<string> declarations,
        string property,
        CssColor? color)
    {
        if (color is { } value && IsValid(value))
        {
            declarations.Add($"{property}:{value}");
        }
    }

    private static void AddBackgroundLayers(
        ICollection<string> declarations,
        LinearGradient? gradient,
        BackgroundImageStyle? image,
        ContentDirection direction)
    {
        var layers = new List<string>();
        if (TryRenderGradient(gradient, out var gradientCss))
        {
            layers.Add(gradientCss);
        }

        if (TryRenderBackgroundImage(image, out var imageUrl) &&
            image is { } value)
        {
            layers.Add($"url(\"{imageUrl}\")");
            declarations.Add($"background-size:{value.Size.ToString().ToLowerInvariant()}");
            declarations.Add(
                $"background-position:{RenderPosition(value.Position, direction)}");
            declarations.Add($"background-repeat:{RenderRepeat(value.Repeat)}");
        }

        if (layers.Count > 0)
        {
            declarations.Add($"background-image:{string.Join(',', layers)}");
        }
    }

    private static bool TryRenderGradient(
        LinearGradient? gradient,
        out string css)
    {
        css = string.Empty;
        if (gradient is not { Enabled: true } value ||
            value.Angle is < 0m or > 360m ||
            value.StartPosition is < 0m or > 100m ||
            value.EndPosition is < 0m or > 100m ||
            value.StartPosition > value.EndPosition ||
            !Enum.IsDefined(value.Type) ||
            !Enum.IsDefined(value.RadialShape) ||
            !Enum.IsDefined(value.RadialPosition) ||
            !IsValid(value.StartColor) ||
            !IsValid(value.EndColor))
        {
            return false;
        }

        var start = $"{value.StartColor} {FormatPercent(value.StartPosition)}";
        var end = $"{value.EndColor} {FormatPercent(value.EndPosition)}";
        css = value.Type switch
        {
            GradientType.Radial =>
                $"radial-gradient({RenderRadialShape(value.RadialShape)} at {RenderRadialPosition(value.RadialPosition)},{start},{end})",
            _ =>
                $"linear-gradient({value.Angle.ToString(CultureInfo.InvariantCulture)}deg,{start},{end})"
        };

        return true;
    }

    private static string RenderRadialShape(RadialGradientShape shape) =>
        shape == RadialGradientShape.Circle ? "circle" : "ellipse";

    private static string RenderRadialPosition(RadialGradientPosition position) =>
        position switch
        {
            RadialGradientPosition.Top => "top",
            RadialGradientPosition.TopRight => "top right",
            RadialGradientPosition.Right => "right",
            RadialGradientPosition.BottomRight => "bottom right",
            RadialGradientPosition.Bottom => "bottom",
            RadialGradientPosition.BottomLeft => "bottom left",
            RadialGradientPosition.Left => "left",
            RadialGradientPosition.TopLeft => "top left",
            _ => "center"
        };

    private static void AddShadows(
        ICollection<string> declarations,
        BoxShadow? shadow,
        CssColor? overlayColor)
    {
        var shadows = new List<string>();
        if (overlayColor is { } overlay && IsValid(overlay))
        {
            shadows.Add($"inset 0 0 0 100vmax {overlay}");
        }

        if (shadow is { Enabled: true } value &&
            value.OffsetX is >= -200m and <= 200m &&
            value.OffsetY is >= -200m and <= 200m &&
            value.Blur is >= 0m and <= 200m &&
            value.Spread is >= -100m and <= 100m &&
            IsValid(value.Color))
        {
            shadows.Add(
                $"{FormatPixels(value.OffsetX)} {FormatPixels(value.OffsetY)} {FormatPixels(value.Blur)} {FormatPixels(value.Spread)} {value.Color}");
        }

        if (shadows.Count > 0)
        {
            declarations.Add($"box-shadow:{string.Join(',', shadows)}");
        }
    }

    private static bool TryRenderBackgroundImage(
        BackgroundImageStyle? image,
        out string url)
    {
        url = string.Empty;
        if (image is not { Enabled: true } value ||
            !TryNormalizeImageUrl(value.Url, out url) ||
            !Enum.IsDefined(value.Size) ||
            !Enum.IsDefined(value.Repeat) ||
            !Enum.IsDefined(value.Position))
        {
            return false;
        }

        return true;
    }

    private static string RenderRepeat(BackgroundImageRepeat repeat) =>
        repeat switch
        {
            BackgroundImageRepeat.NoRepeat => "no-repeat",
            BackgroundImageRepeat.Repeat => "repeat",
            BackgroundImageRepeat.RepeatX => "repeat-x",
            BackgroundImageRepeat.RepeatY => "repeat-y",
            _ => "no-repeat"
        };

    private static string RenderPosition(
        BackgroundImagePosition position,
        ContentDirection direction)
    {
        var inlineStart = direction == ContentDirection.RightToLeft
            ? "right"
            : "left";
        var inlineEnd = direction == ContentDirection.RightToLeft
            ? "left"
            : "right";

        return
        position switch
        {
            BackgroundImagePosition.BlockStartInlineStart => $"top {inlineStart}",
            BackgroundImagePosition.BlockStartCenter => "top center",
            BackgroundImagePosition.BlockStartInlineEnd => $"top {inlineEnd}",
            BackgroundImagePosition.CenterInlineStart => $"center {inlineStart}",
            BackgroundImagePosition.Center => "center",
            BackgroundImagePosition.CenterInlineEnd => $"center {inlineEnd}",
            BackgroundImagePosition.BlockEndInlineStart => $"bottom {inlineStart}",
            BackgroundImagePosition.BlockEndCenter => "bottom center",
            BackgroundImagePosition.BlockEndInlineEnd => $"bottom {inlineEnd}",
            _ => "center"
        };
    }

    private static bool TryNormalizeImageUrl(string value, out string normalized)
    {
        normalized = value.Trim();
        if (normalized.StartsWith('/') &&
            !normalized.Contains('"') &&
            !normalized.Contains('\'') &&
            !normalized.Contains('(') &&
            !normalized.Contains(')'))
        {
            return true;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = uri.AbsoluteUri;
        return !normalized.Contains('"') && !normalized.Contains('\'');
    }

    private static string FormatPixels(decimal value) =>
        $"{value.ToString(CultureInfo.InvariantCulture)}px";

    private static string FormatPercent(decimal value) =>
        $"{value.ToString(CultureInfo.InvariantCulture)}%";

    private static void AddSpacing(
        ICollection<string> declarations,
        string prefix,
        LogicalSpacing spacing)
    {
        AddLength(declarations, $"{prefix}-block-start", spacing.BlockStart);
        AddLength(declarations, $"{prefix}-block-end", spacing.BlockEnd);
        AddLength(declarations, $"{prefix}-inline-start", spacing.InlineStart);
        AddLength(declarations, $"{prefix}-inline-end", spacing.InlineEnd);
    }

    private static void AddLength(
        ICollection<string> declarations,
        string property,
        CssLength? length)
    {
        if (length is not { } value || !IsValid(value))
        {
            return;
        }

        declarations.Add($"{property}:{value}");
    }

    private static bool IsValid(CssLength length)
    {
        if (length.Unit == CssLengthUnit.Auto)
        {
            return length.Value is null;
        }

        return length.Value is >= 0m and <= 100_000m &&
               Enum.IsDefined(length.Unit);
    }

    private static bool IsValid(CssColor color)
        => CssColorValidator.IsValid(color);
}
