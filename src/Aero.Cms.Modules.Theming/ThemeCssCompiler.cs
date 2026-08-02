using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Theming;

namespace Aero.Cms.Modules.Theming;

public sealed record CompiledThemeCss(string Css, string Sha256);
public interface IThemeCssCompiler
{
    CompiledThemeCss Compile(string dataThemeName, ThemeTokenSet tokens);
    CompiledThemeCss CompilePreview(string dataThemeName, ThemeTokenSet tokens);
}

/// <summary>Produces byte-stable CSS from the closed token document.</summary>
public sealed class ThemeCssCompiler : IThemeCssCompiler
{
    public CompiledThemeCss Compile(string dataThemeName, ThemeTokenSet tokens)
        => CompileCore(dataThemeName, tokens, requirePublishable: true);

    public CompiledThemeCss CompilePreview(string dataThemeName, ThemeTokenSet tokens)
        => CompileCore(dataThemeName, tokens, requirePublishable: false);

    private static CompiledThemeCss CompileCore(
        string dataThemeName,
        ThemeTokenSet tokens,
        bool requirePublishable)
    {
        if (!IsDataThemeName(dataThemeName)) throw new ArgumentException("The data-theme name must be lowercase alphanumeric with hyphens.", nameof(dataThemeName));
        ArgumentNullException.ThrowIfNull(tokens);
        if (requirePublishable)
        {
            ThemeTokenValidator.ThrowIfPublishable(tokens);
        }
        else
        {
            ThemeTokenValidator.ThrowIfInvalid(tokens);
        }

        var css = Render(dataThemeName, tokens.Light, tokens.Shape, "light") + Render(dataThemeName + "-dark", tokens.Dark, tokens.Shape, "dark");
        var bytes = new UTF8Encoding(false).GetBytes(css);
        return new(css, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }
    private static string Render(string name, ThemeColorTokens c, ThemeShapeTokens s, string scheme) => string.Create(CultureInfo.InvariantCulture, $"[data-theme={name}]{{color-scheme:{scheme};--color-base-100:{c.Base100};--color-base-200:{c.Base200};--color-base-300:{c.Base300};--color-base-content:{c.BaseContent};--color-primary:{c.Primary};--color-primary-content:{c.PrimaryContent};--color-secondary:{c.Secondary};--color-secondary-content:{c.SecondaryContent};--color-accent:{c.Accent};--color-accent-content:{c.AccentContent};--color-neutral:{c.Neutral};--color-neutral-content:{c.NeutralContent};--color-info:{c.Info};--color-info-content:{c.InfoContent};--color-success:{c.Success};--color-success-content:{c.SuccessContent};--color-warning:{c.Warning};--color-warning-content:{c.WarningContent};--color-error:{c.Error};--color-error-content:{c.ErrorContent};--radius-selector:{s.RadiusSelectorRem}rem;--radius-field:{s.RadiusFieldRem}rem;--radius-box:{s.RadiusBoxRem}rem;--size-selector:{s.SizeSelectorRem}rem;--size-field:{s.SizeFieldRem}rem;--border:{s.BorderRem}rem;--depth:{s.Depth};--noise:{s.Noise};}}\n");
    internal static bool IsDataThemeName(string value) => !string.IsNullOrWhiteSpace(value) && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}

public static class ThemeTokenValidator
{
    public static void ThrowIfInvalid(ThemeTokenSet tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Light is null || tokens.Dark is null || tokens.Shape is null)
        {
            throw new ArgumentException("Theme light, dark, and shape token groups are required.", nameof(tokens));
        }

        foreach (var value in Colors(tokens.Light).Concat(Colors(tokens.Dark))) if (!IsHex(value)) throw new ArgumentException("Theme colors must be six-digit sRGB hexadecimal values.");
        if (tokens.Shape.Depth is < 0 or > 1 || tokens.Shape.Noise is < 0 or > 1) throw new ArgumentException("Theme depth and noise must be 0 or 1.");
        if (new[] { tokens.Shape.RadiusSelectorRem, tokens.Shape.RadiusFieldRem, tokens.Shape.RadiusBoxRem, tokens.Shape.SizeSelectorRem, tokens.Shape.SizeFieldRem, tokens.Shape.BorderRem }.Any(x => x < 0)) throw new ArgumentException("Theme shape values cannot be negative.");
    }
    public static void ThrowIfPublishable(ThemeTokenSet tokens)
    {
        ThrowIfInvalid(tokens);
        if (GetContrastWarnings(tokens).Count != 0) throw new ArgumentException("Theme foreground/background color pairs must meet a 4.5:1 contrast ratio.");
    }
    public static IReadOnlyList<ThemeValidationWarning> GetContrastWarnings(ThemeTokenSet tokens)
    {
        ThrowIfInvalid(tokens);
        return ContrastPairs(tokens.Light, "light").Concat(ContrastPairs(tokens.Dark, "dark")).Where(x => x.Ratio < 4.5).Select(x => new ThemeValidationWarning("contrast", $"{x.Name} ({x.Mode}) is {x.Ratio:F2}:1; publish requires 4.5:1.")).ToArray();
    }
    private static IEnumerable<string> Colors(ThemeColorTokens c) => [c.Base100, c.Base200, c.Base300, c.BaseContent, c.Primary, c.PrimaryContent, c.Secondary, c.SecondaryContent, c.Accent, c.AccentContent, c.Neutral, c.NeutralContent, c.Info, c.InfoContent, c.Success, c.SuccessContent, c.Warning, c.WarningContent, c.Error, c.ErrorContent];
    private static bool IsHex(string? value) => value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
    private static IEnumerable<(string Name, string Mode, double Ratio)> ContrastPairs(ThemeColorTokens tokens, string mode)
    { foreach (var pair in new[] { ("base 100", tokens.Base100, tokens.BaseContent), ("base 200", tokens.Base200, tokens.BaseContent), ("base 300", tokens.Base300, tokens.BaseContent), ("primary", tokens.Primary, tokens.PrimaryContent), ("secondary", tokens.Secondary, tokens.SecondaryContent), ("accent", tokens.Accent, tokens.AccentContent), ("neutral", tokens.Neutral, tokens.NeutralContent), ("info", tokens.Info, tokens.InfoContent), ("success", tokens.Success, tokens.SuccessContent), ("warning", tokens.Warning, tokens.WarningContent), ("error", tokens.Error, tokens.ErrorContent) }) yield return (pair.Item1, mode, Contrast(pair.Item2, pair.Item3)); }
    private static double Contrast(string left, string right) { var a = Luminance(left); var b = Luminance(right); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05); }
    private static double Luminance(string value)
    {
        var red = Linear(Convert.ToInt32(value.Substring(1, 2), 16) / 255d); var green = Linear(Convert.ToInt32(value.Substring(3, 2), 16) / 255d); var blue = Linear(Convert.ToInt32(value.Substring(5, 2), 16) / 255d);
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }
    private static double Linear(double channel) => channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4);
}
