using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Theming;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

public enum ThemeStudioPanel { Components, Patterns }
public enum ThemeStudioViewport { Phone, Tablet, Desktop }

public sealed record ThemeColorChange(ThemeDefaultMode Mode, string Token, string Value);
public sealed record ThemeShapeChange(string Token, decimal Value);
public sealed record ThemeAssignmentRequest(string ThemeId, string Version);

public sealed record ThemeContrastResult(string Label, ThemeDefaultMode Mode, double Ratio)
{
    public bool Passes => Ratio >= 4.5d;
}

internal static class ThemeStudioTokens
{
    public static ThemeTokenSet Clone(ThemeTokenSet source) => new()
    {
        Light = Clone(source.Light),
        Dark = Clone(source.Dark),
        Shape = new ThemeShapeTokens
        {
            RadiusSelectorRem = source.Shape.RadiusSelectorRem,
            RadiusFieldRem = source.Shape.RadiusFieldRem,
            RadiusBoxRem = source.Shape.RadiusBoxRem,
            SizeSelectorRem = source.Shape.SizeSelectorRem,
            SizeFieldRem = source.Shape.SizeFieldRem,
            BorderRem = source.Shape.BorderRem,
            Depth = source.Shape.Depth,
            Noise = source.Shape.Noise
        },
        DefaultMode = source.DefaultMode
    };

    public static ThemeColorTokens Clone(ThemeColorTokens source) => new()
    {
        Base100 = source.Base100, Base200 = source.Base200, Base300 = source.Base300, BaseContent = source.BaseContent,
        Primary = source.Primary, PrimaryContent = source.PrimaryContent,
        Secondary = source.Secondary, SecondaryContent = source.SecondaryContent,
        Accent = source.Accent, AccentContent = source.AccentContent,
        Neutral = source.Neutral, NeutralContent = source.NeutralContent,
        Info = source.Info, InfoContent = source.InfoContent,
        Success = source.Success, SuccessContent = source.SuccessContent,
        Warning = source.Warning, WarningContent = source.WarningContent,
        Error = source.Error, ErrorContent = source.ErrorContent
    };

    public static IReadOnlyList<ThemeContrastResult> Contrast(ThemeTokenSet tokens) =>
        Pairs(tokens.Light, ThemeDefaultMode.Light).Concat(Pairs(tokens.Dark, ThemeDefaultMode.Dark)).ToArray();

    public static IReadOnlyList<string> Validate(ThemeTokenSet tokens)
    {
        var problems = new List<string>();
        foreach (var (name, value) in Colors(tokens.Light, "Light").Concat(Colors(tokens.Dark, "Dark")))
        {
            if (!IsHex(value)) problems.Add($"{name} must be a six-digit hexadecimal color.");
        }

        if (new[] { tokens.Shape.RadiusSelectorRem, tokens.Shape.RadiusFieldRem, tokens.Shape.RadiusBoxRem,
                    tokens.Shape.SizeSelectorRem, tokens.Shape.SizeFieldRem, tokens.Shape.BorderRem }.Any(static value => value < 0))
            problems.Add("Shape measurements cannot be negative.");
        if (tokens.Shape.Depth is < 0 or > 1 || tokens.Shape.Noise is < 0 or > 1)
            problems.Add("Depth and noise must be either 0 or 1.");

        if (problems.Count == 0)
        {
            problems.AddRange(Contrast(tokens).Where(static result => !result.Passes)
                .Select(static result => $"{result.Label} ({result.Mode.ToString().ToLowerInvariant()}) is {result.Ratio:F2}:1; publishing requires 4.5:1."));
        }

        return problems;
    }

    public static string PreviewCss(ThemeTokenSet tokens)
    {
        var builder = new StringBuilder(1600);
        AppendMode(builder, "theme-studio-light", tokens.Light, tokens.Shape, "light");
        AppendMode(builder, "theme-studio-dark", tokens.Dark, tokens.Shape, "dark");
        return builder.ToString();
    }

    public static bool IsHex(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    private static IEnumerable<ThemeContrastResult> Pairs(ThemeColorTokens colors, ThemeDefaultMode mode)
    {
        yield return Result("Base", colors.Base100, colors.BaseContent, mode);
        yield return Result("Primary", colors.Primary, colors.PrimaryContent, mode);
        yield return Result("Secondary", colors.Secondary, colors.SecondaryContent, mode);
        yield return Result("Accent", colors.Accent, colors.AccentContent, mode);
        yield return Result("Neutral", colors.Neutral, colors.NeutralContent, mode);
        yield return Result("Info", colors.Info, colors.InfoContent, mode);
        yield return Result("Success", colors.Success, colors.SuccessContent, mode);
        yield return Result("Warning", colors.Warning, colors.WarningContent, mode);
        yield return Result("Error", colors.Error, colors.ErrorContent, mode);
    }

    private static ThemeContrastResult Result(string label, string background, string foreground, ThemeDefaultMode mode) =>
        new(label, mode, IsHex(background) && IsHex(foreground) ? ContrastRatio(background, foreground) : 0d);

    private static double ContrastRatio(string left, string right)
    {
        var first = Luminance(left);
        var second = Luminance(right);
        return (Math.Max(first, second) + .05d) / (Math.Min(first, second) + .05d);
    }

    private static double Luminance(string value)
    {
        static double Linear(double channel) => channel <= .04045d ? channel / 12.92d : Math.Pow((channel + .055d) / 1.055d, 2.4d);
        var red = Linear(Convert.ToInt32(value.Substring(1, 2), 16) / 255d);
        var green = Linear(Convert.ToInt32(value.Substring(3, 2), 16) / 255d);
        var blue = Linear(Convert.ToInt32(value.Substring(5, 2), 16) / 255d);
        return .2126d * red + .7152d * green + .0722d * blue;
    }

    private static IEnumerable<(string Name, string Value)> Colors(ThemeColorTokens colors, string mode)
    {
        yield return ($"{mode} base 100", colors.Base100); yield return ($"{mode} base 200", colors.Base200);
        yield return ($"{mode} base 300", colors.Base300); yield return ($"{mode} base content", colors.BaseContent);
        yield return ($"{mode} primary", colors.Primary); yield return ($"{mode} primary content", colors.PrimaryContent);
        yield return ($"{mode} secondary", colors.Secondary); yield return ($"{mode} secondary content", colors.SecondaryContent);
        yield return ($"{mode} accent", colors.Accent); yield return ($"{mode} accent content", colors.AccentContent);
        yield return ($"{mode} neutral", colors.Neutral); yield return ($"{mode} neutral content", colors.NeutralContent);
        yield return ($"{mode} info", colors.Info); yield return ($"{mode} info content", colors.InfoContent);
        yield return ($"{mode} success", colors.Success); yield return ($"{mode} success content", colors.SuccessContent);
        yield return ($"{mode} warning", colors.Warning); yield return ($"{mode} warning content", colors.WarningContent);
        yield return ($"{mode} error", colors.Error); yield return ($"{mode} error content", colors.ErrorContent);
    }

    private static void AppendMode(StringBuilder builder, string name, ThemeColorTokens c, ThemeShapeTokens s, string scheme)
    {
        static string Color(string value) => IsHex(value) ? value : "#000000";
        builder.Append("[data-theme=").Append(name).Append("]{color-scheme:").Append(scheme)
            .Append(";--color-base-100:").Append(Color(c.Base100)).Append(";--color-base-200:").Append(Color(c.Base200))
            .Append(";--color-base-300:").Append(Color(c.Base300)).Append(";--color-base-content:").Append(Color(c.BaseContent))
            .Append(";--color-primary:").Append(Color(c.Primary)).Append(";--color-primary-content:").Append(Color(c.PrimaryContent))
            .Append(";--color-secondary:").Append(Color(c.Secondary)).Append(";--color-secondary-content:").Append(Color(c.SecondaryContent))
            .Append(";--color-accent:").Append(Color(c.Accent)).Append(";--color-accent-content:").Append(Color(c.AccentContent))
            .Append(";--color-neutral:").Append(Color(c.Neutral)).Append(";--color-neutral-content:").Append(Color(c.NeutralContent))
            .Append(";--color-info:").Append(Color(c.Info)).Append(";--color-info-content:").Append(Color(c.InfoContent))
            .Append(";--color-success:").Append(Color(c.Success)).Append(";--color-success-content:").Append(Color(c.SuccessContent))
            .Append(";--color-warning:").Append(Color(c.Warning)).Append(";--color-warning-content:").Append(Color(c.WarningContent))
            .Append(";--color-error:").Append(Color(c.Error)).Append(";--color-error-content:").Append(Color(c.ErrorContent))
            .Append(";--radius-selector:").Append(s.RadiusSelectorRem.ToString(CultureInfo.InvariantCulture)).Append("rem")
            .Append(";--radius-field:").Append(s.RadiusFieldRem.ToString(CultureInfo.InvariantCulture)).Append("rem")
            .Append(";--radius-box:").Append(s.RadiusBoxRem.ToString(CultureInfo.InvariantCulture)).Append("rem")
            .Append(";--size-selector:").Append(s.SizeSelectorRem.ToString(CultureInfo.InvariantCulture)).Append("rem")
            .Append(";--size-field:").Append(s.SizeFieldRem.ToString(CultureInfo.InvariantCulture)).Append("rem")
            .Append(";--border:").Append(s.BorderRem.ToString(CultureInfo.InvariantCulture)).Append("rem")
            .Append(";--depth:").Append(s.Depth).Append(";--noise:").Append(s.Noise).Append(";}\n");
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ThemeImportEnvelope))]
internal partial class ThemeStudioJsonContext : JsonSerializerContext;
