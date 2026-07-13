using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Compiles semantic layout intent into deterministic, scoped native CSS.
/// </summary>
public sealed class NativeCssStyleCompiler : IStyleCompiler
{
    private const string CssNewLine = "\n";

    public Result<CompiledPageStyles> Compile(HtmlPageContent content, IStyleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(profile);

        if (content.Root.Kind is not HtmlNodeKind.Fragment)
        {
            return AeroError.ValidationError(["Page content must begin with a fragment root before styles can be compiled."]);
        }

        if (profile.SmallScreenBreakpointRem <= 0)
        {
            return AeroError.ValidationError(["The small-screen breakpoint must be greater than zero."]);
        }

        if (!HtmlTreeOperations.HasUniqueNodeIds(content.Root))
        {
            return AeroError.ValidationError(["Page node identities must be unique before styles can be compiled."]);
        }

        var nodeClasses = new Dictionary<long, IReadOnlyList<string>>();
        var rules = new Dictionary<string, string>(StringComparer.Ordinal);
        var validationErrors = new List<string>();
        CompileNode(content.Root, profile, nodeClasses, rules, validationErrors);

        if (validationErrors.Count > 0)
        {
            return AeroError.ValidationError(validationErrors);
        }

        var css = string.Join(CssNewLine, rules.OrderBy(rule => rule.Key).Select(rule => rule.Value));
        return new Result<CompiledPageStyles>.Ok(new CompiledPageStyles
        {
            NodeClasses = nodeClasses,
            CssText = css,
            ContentHash = Hash(css),
            ProfileId = profile.ProfileId,
            ProfileVersion = profile.ProfileVersion
        });
    }

    private static void CompileNode(
        HtmlNode node,
        IStyleProfile profile,
        IDictionary<long, IReadOnlyList<string>> nodeClasses,
        IDictionary<string, string> rules,
        ICollection<string> errors)
    {
        if (node.Style is not null)
        {
            if (node.Kind is not HtmlNodeKind.Element)
            {
                errors.Add($"Only element nodes can carry style intent; node {node.NodeId} is {node.Kind}.");
            }
            else
            {
                var compiled = CompileStyle(node.Style, profile, errors);
                if (compiled is not null && compiled.Value.Declarations.Length > 0)
                {
                    var fingerprint = $"{profile.ProfileId}|{profile.ProfileVersion}|{Format(profile.SmallScreenBreakpointRem)}|{compiled.Value.Canonical}";
                    var className = $"aero-s-{Hash(fingerprint)[..12]}";
                    nodeClasses[node.NodeId] = [className];
                    rules.TryAdd(className, BuildRule(className, compiled.Value, profile));
                }
            }
        }

        foreach (var child in node.Children)
        {
            CompileNode(child, profile, nodeClasses, rules, errors);
        }
    }

    private static CompiledStyle? CompileStyle(HtmlStyle style, IStyleProfile profile, ICollection<string> errors)
    {
        if (style.GridColumns is < 1 or > 12)
        {
            errors.Add("Grid columns must be between 1 and 12.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(style.Surface?.BackgroundImageUrl)
            && style.Typography?.Gradient is not null)
        {
            errors.Add("A node cannot combine a surface background image with a text gradient because both require background-image.");
            return null;
        }

        var declarations = new List<string>();
        Add(declarations, "display", Display(style.Display));
        Add(declarations, "flex-direction", FlexDirection(style.FlexDirection));

        if (style.GridColumns is { } columns)
        {
            if (style.Display is not (CssDisplay.Grid or CssDisplay.InlineGrid))
            {
                errors.Add("Grid columns require grid or inline-grid display.");
                return null;
            }

            Add(declarations, "grid-template-columns", $"repeat({columns}, minmax(0, 1fr))");
        }

        AddLength(declarations, "gap", style.Gap, allowNegative: false, errors);
        Add(declarations, "align-items", Alignment(style.AlignItems));
        Add(declarations, "justify-content", Justification(style.JustifyContent));
        AddSpacing(declarations, "padding", style.Padding, allowNegative: false, errors);
        AddSpacing(declarations, "margin", style.Margin, allowNegative: false, errors);
        AddLength(declarations, "min-height", style.MinimumHeight, allowNegative: false, errors);
        AddSurface(declarations, style.Surface, profile, errors);
        AddTypography(declarations, style.Typography, profile, errors);

        var responsive = style.StackOnSmallScreens
            ? style.Display switch
            {
                CssDisplay.Grid or CssDisplay.InlineGrid => "grid-template-columns: minmax(0, 1fr);",
                CssDisplay.Flex or CssDisplay.InlineFlex => "flex-direction: column;",
                _ => null
            }
            : null;

        if (style.StackOnSmallScreens && responsive is null)
        {
            errors.Add("Small-screen stacking requires grid or flex display.");
            return null;
        }

        var declarationText = string.Join(' ', declarations);
        return new CompiledStyle(declarationText, $"{declarationText}|{responsive}", responsive);
    }

    private static string BuildRule(string className, CompiledStyle style, IStyleProfile profile)
    {
        var rule = $".{className} {{ {style.Declarations} }}";
        return style.ResponsiveDeclarations is null
            ? rule
            : $"{rule}{CssNewLine}@media (max-width: {Format(profile.SmallScreenBreakpointRem)}rem) {{ .{className} {{ {style.ResponsiveDeclarations} }} }}";
    }

    private static void Add(ICollection<string> declarations, string property, string? value)
    {
        if (value is not null) declarations.Add($"{property}: {value};");
    }

    private static void AddSpacing(ICollection<string> declarations, string prefix, CssLogicalSpacing? spacing, bool allowNegative, ICollection<string> errors)
    {
        if (spacing is null) return;
        AddLength(declarations, $"{prefix}-block-start", spacing.BlockStart, allowNegative, errors);
        AddLength(declarations, $"{prefix}-inline-end", spacing.InlineEnd, allowNegative, errors);
        AddLength(declarations, $"{prefix}-block-end", spacing.BlockEnd, allowNegative, errors);
        AddLength(declarations, $"{prefix}-inline-start", spacing.InlineStart, allowNegative, errors);
    }

    private static void AddLength(ICollection<string> declarations, string property, CssLength? length, bool allowNegative, ICollection<string> errors)
    {
        if (length is null) return;
        if (length.Unit is < CssLengthUnit.Pixel or > CssLengthUnit.ViewportWidth)
        {
            errors.Add($"{property} uses an unsupported length unit.");
            return;
        }

        if (!allowNegative && length.Value < 0)
        {
            errors.Add($"{property} cannot be negative.");
            return;
        }

        Add(declarations, property, $"{Format(length.Value)}{Unit(length.Unit)}");
    }

    private static void AddSurface(
        ICollection<string> declarations,
        CssSurfaceStyle? surface,
        IStyleProfile profile,
        ICollection<string> errors)
    {
        if (surface is null) return;

        var backgroundColor = ResolveColor(surface.BackgroundColor, profile, "background color", errors);
        Add(declarations, "background-color", backgroundColor);

        var hasImage = !string.IsNullOrWhiteSpace(surface.BackgroundImageUrl);
        if (hasImage && !HtmlUrlPolicy.IsSafeMediaUrl(surface.BackgroundImageUrl!))
        {
            errors.Add("The background image URL is not a supported media URL.");
        }

        if (surface.OverlayColor is null != surface.OverlayOpacity is null)
        {
            errors.Add("A background overlay requires both a color and an opacity.");
        }

        if ((surface.OverlayColor is not null || surface.OverlayOpacity is not null) && !hasImage)
        {
            errors.Add("A background overlay requires a background image.");
        }

        string? backgroundImage = hasImage
            ? $"url(\"{EscapeCssString(surface.BackgroundImageUrl!)}\")"
            : null;

        if (surface.OverlayColor is not null && surface.OverlayOpacity is { } opacity)
        {
            if (opacity is < 0 or > 1)
            {
                errors.Add("Background overlay opacity must be between zero and one.");
            }
            else
            {
                var overlay = ResolveOverlayColor(surface.OverlayColor, opacity, profile, errors);
                if (overlay is not null && backgroundImage is not null)
                {
                    backgroundImage = $"linear-gradient({overlay}, {overlay}), {backgroundImage}";
                }
            }
        }

        Add(declarations, "background-image", backgroundImage);
        Add(declarations, "background-size", BackgroundFit(surface.BackgroundFit, errors));
        Add(declarations, "background-position", BackgroundPosition(surface.BackgroundPosition, errors));
        Add(declarations, "background-repeat", BackgroundRepeat(surface.BackgroundRepeat, errors));
        AddLength(declarations, "border-radius", surface.BorderRadius, allowNegative: false, errors);

        if (!hasImage && (surface.BackgroundFit is not null || surface.BackgroundPosition is not null || surface.BackgroundRepeat is not null))
        {
            errors.Add("Background fit, position, and repeat require a background image.");
        }
    }

    private static void AddTypography(
        ICollection<string> declarations,
        CssTypographyStyle? typography,
        IStyleProfile profile,
        ICollection<string> errors)
    {
        if (typography is null) return;

        if (typography.FontWeight is { } weight && (weight is < 100 or > 900 || weight % 100 != 0))
        {
            errors.Add("Font weight must be a multiple of 100 between 100 and 900.");
        }
        else if (typography.FontWeight is { } validWeight)
        {
            Add(declarations, "font-weight", validWeight.ToString(CultureInfo.InvariantCulture));
        }

        if (typography.LineHeight is <= 0)
        {
            errors.Add("Line height must be greater than zero.");
        }
        else if (typography.LineHeight is { } lineHeight)
        {
            Add(declarations, "line-height", Format(lineHeight));
        }

        AddLength(declarations, "font-size", typography.FontSize, allowNegative: false, errors);
        AddLength(declarations, "letter-spacing", typography.LetterSpacing, allowNegative: true, errors);
        Add(declarations, "text-align", TextAlignment(typography.Alignment, errors));

        if (typography.Color is not null && typography.Gradient is not null)
        {
            errors.Add("Text color and text gradient are mutually exclusive style intents.");
            return;
        }

        Add(declarations, "color", ResolveColor(typography.Color, profile, "text color", errors));

        if (typography.Gradient is not { } gradient) return;
        if (gradient.AngleDegrees is < 0 or > 360)
        {
            errors.Add("Text gradient angle must be between zero and 360 degrees.");
            return;
        }

        var start = ResolveColor(gradient.StartColor, profile, "text gradient start color", errors);
        var end = ResolveColor(gradient.EndColor, profile, "text gradient end color", errors);
        if (start is null || end is null) return;

        Add(declarations, "background-image", $"linear-gradient({Format(gradient.AngleDegrees)}deg, {start}, {end})");
        Add(declarations, "background-clip", "text");
        Add(declarations, "-webkit-background-clip", "text");
        Add(declarations, "color", "transparent");
    }

    private static string? ResolveColor(CssColor? color, IStyleProfile profile, string description, ICollection<string> errors)
    {
        if (color is null) return null;

        string? raw = color.Kind switch
        {
            CssColorKind.Hex => color.Value,
            CssColorKind.ThemeToken when profile.ColorTokens.TryGetValue(color.Value, out var tokenValue) => tokenValue,
            CssColorKind.ThemeToken => null,
            _ => null
        };

        if (raw is null)
        {
            errors.Add(color.Kind is CssColorKind.ThemeToken
                ? $"The {description} token '{color.Value}' is not defined by style profile '{profile.ProfileId}'."
                : $"The {description} kind is not supported.");
            return null;
        }

        if (!TryNormalizeHex(raw, out var normalized))
        {
            errors.Add($"The {description} must resolve to a 3, 4, 6, or 8 digit hexadecimal color.");
            return null;
        }

        return normalized;
    }

    private static string? ResolveOverlayColor(CssColor color, decimal opacity, IStyleProfile profile, ICollection<string> errors)
    {
        var resolved = ResolveColor(color, profile, "overlay color", errors);
        if (resolved is null) return null;

        var digits = resolved[1..];
        var red = Convert.ToByte(digits[..2], 16);
        var green = Convert.ToByte(digits.Substring(2, 2), 16);
        var blue = Convert.ToByte(digits.Substring(4, 2), 16);
        var sourceAlpha = digits.Length == 8 ? Convert.ToByte(digits.Substring(6, 2), 16) / 255m : 1m;
        return $"rgba({red}, {green}, {blue}, {Format(sourceAlpha * opacity)})";
    }

    private static bool TryNormalizeHex(string value, out string normalized)
    {
        normalized = string.Empty;
        if (value.Length is not (4 or 5 or 7 or 9) || value[0] != '#'
            || value.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF"))
        {
            return false;
        }

        var digits = value[1..].ToLowerInvariant();
        if (digits.Length is 3 or 4)
        {
            digits = string.Concat(digits.Select(character => $"{character}{character}"));
        }

        normalized = $"#{digits}";
        return true;
    }

    private static string? BackgroundFit(CssBackgroundFit? value, ICollection<string> errors) => value switch
    {
        null => null,
        CssBackgroundFit.Cover => "cover",
        CssBackgroundFit.Contain => "contain",
        _ => AddUnsupported<CssBackgroundFit>("background fit", errors)
    };

    private static string? BackgroundPosition(CssBackgroundPosition? value, ICollection<string> errors) => value switch
    {
        null => null,
        CssBackgroundPosition.Center => "center",
        CssBackgroundPosition.Top => "top",
        CssBackgroundPosition.Bottom => "bottom",
        _ => AddUnsupported<CssBackgroundPosition>("background position", errors)
    };

    private static string? BackgroundRepeat(CssBackgroundRepeat? value, ICollection<string> errors) => value switch
    {
        null => null,
        CssBackgroundRepeat.NoRepeat => "no-repeat",
        CssBackgroundRepeat.Repeat => "repeat",
        CssBackgroundRepeat.RepeatX => "repeat-x",
        CssBackgroundRepeat.RepeatY => "repeat-y",
        _ => AddUnsupported<CssBackgroundRepeat>("background repeat", errors)
    };

    private static string? TextAlignment(CssTextAlignment? value, ICollection<string> errors) => value switch
    {
        null => null,
        CssTextAlignment.Start => "start",
        CssTextAlignment.Center => "center",
        CssTextAlignment.End => "end",
        CssTextAlignment.Justify => "justify",
        _ => AddUnsupported<CssTextAlignment>("text alignment", errors)
    };

    private static string? AddUnsupported<T>(string description, ICollection<string> errors)
    {
        errors.Add($"The {description} value is not supported.");
        return null;
    }

    private static string EscapeCssString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string? Display(CssDisplay? value) => value?.ToString() switch
    {
        "InlineBlock" => "inline-block", "InlineFlex" => "inline-flex", "InlineGrid" => "inline-grid",
        { } text => text.ToLowerInvariant(), _ => null
    };

    private static string? FlexDirection(CssFlexDirection? value) => value switch
    {
        CssFlexDirection.Row => "row", CssFlexDirection.RowReverse => "row-reverse",
        CssFlexDirection.Column => "column", CssFlexDirection.ColumnReverse => "column-reverse", _ => null
    };

    private static string? Alignment(CssAlignment? value) => value?.ToString().ToLowerInvariant();
    private static string? Justification(CssJustification? value) => value switch
    {
        CssJustification.SpaceBetween => "space-between", CssJustification.SpaceAround => "space-around",
        CssJustification.SpaceEvenly => "space-evenly", { } other => other.ToString().ToLowerInvariant(), _ => null
    };

    private static string Unit(CssLengthUnit unit) => unit switch
    {
        CssLengthUnit.Pixel => "px", CssLengthUnit.Rem => "rem", CssLengthUnit.Em => "em",
        CssLengthUnit.Percent => "%", CssLengthUnit.ViewportHeight => "vh", CssLengthUnit.ViewportWidth => "vw",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };

    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private readonly record struct CompiledStyle(string Declarations, string Canonical, string? ResponsiveDeclarations);
}
