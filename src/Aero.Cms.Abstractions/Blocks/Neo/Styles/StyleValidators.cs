using FluentValidation;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

public sealed class CssLengthValidator : AbstractValidator<CssLength>
{
    public CssLengthValidator()
    {
        RuleFor(length => length)
            .Must(length => length.Unit == CssLengthUnit.Auto
                ? length.Value is null
                : length.Value is not null)
            .WithMessage("Auto lengths must not have a value and other units require a value.");

        RuleFor(length => length.Value)
            .GreaterThanOrEqualTo(0)
            .When(length => length.Value is not null)
            .WithMessage("CSS length values cannot be negative.");
    }
}

public sealed class ResponsiveNodeStyleValidator : AbstractValidator<ResponsiveNodeStyle>
{
    public ResponsiveNodeStyleValidator()
    {
        RuleFor(style => style.Base).SetValidator(new NodeStyleValidator());
        RuleFor(style => style.Tablet).SetValidator(new NodeStyleOverrideValidator()!);
        RuleFor(style => style.Mobile).SetValidator(new NodeStyleOverrideValidator()!);
    }
}

public sealed class NodeStyleValidator : AbstractValidator<NodeStyle>
{
    public NodeStyleValidator()
    {
        RuleFor(style => style.Opacity)
            .InclusiveBetween(0, 1)
            .When(style => style.Opacity is not null);

        RuleFor(style => style.ForegroundColor).Must(BeValidColor);
        RuleFor(style => style.BackgroundColor).Must(BeValidColor);
        RuleFor(style => style.BackgroundOverlayColor).Must(BeValidColor);
        RuleFor(style => style.BackgroundGradient)
            .SetValidator(new LinearGradientValidator()!);
        RuleFor(style => style.BackgroundImage)
            .SetValidator(new BackgroundImageStyleValidator()!);
        RuleFor(style => style.BorderColor).Must(BeValidColor);
        RuleFor(style => style.Shadow).SetValidator(new BoxShadowValidator()!);
        RuleFor(style => style.LineHeight)
            .InclusiveBetween(0.5m, 5m)
            .When(style => style.LineHeight is not null);
        RuleFor(style => style.FontWeight).IsInEnum();
        RuleFor(style => style.TextAlignment).IsInEnum();
        AddLengthRules();
    }

    private void AddLengthRules()
    {
        RuleFor(style => style.Width).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.Height).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.MinimumWidth).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.MaximumWidth).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.MinimumHeight).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.MaximumHeight).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.BorderWidth).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.BorderRadius).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.FontSize).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.LetterSpacing).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.Margin).SetValidator(new LogicalSpacingValidator());
        RuleFor(style => style.Padding).SetValidator(new LogicalSpacingValidator());
    }

    private static bool BeValidColor(CssColor? color) =>
        color is null || CssColorValidator.IsValid(color.Value);
}

public sealed class NodeStyleOverrideValidator : AbstractValidator<NodeStyleOverride>
{
    public NodeStyleOverrideValidator()
    {
        RuleFor(style => style.Opacity)
            .InclusiveBetween(0, 1)
            .When(style => style.Opacity is not null);

        RuleFor(style => style.ForegroundColor).Must(BeValidColor);
        RuleFor(style => style.BackgroundColor).Must(BeValidColor);
        RuleFor(style => style.BackgroundOverlayColor).Must(BeValidColor);
        RuleFor(style => style.BackgroundGradient)
            .SetValidator(new LinearGradientValidator()!);
        RuleFor(style => style.BackgroundImage)
            .SetValidator(new BackgroundImageStyleValidator()!);
        RuleFor(style => style.BorderColor).Must(BeValidColor);
        RuleFor(style => style.Shadow).SetValidator(new BoxShadowValidator()!);
        RuleFor(style => style.LineHeight)
            .InclusiveBetween(0.5m, 5m)
            .When(style => style.LineHeight is not null);
        RuleFor(style => style.FontWeight).IsInEnum()
            .When(style => style.FontWeight is not null);
        RuleFor(style => style.TextAlignment).IsInEnum()
            .When(style => style.TextAlignment is not null);
        RuleFor(style => style.Width).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.Height).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.MinimumWidth).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.MaximumWidth).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.MinimumHeight).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.MaximumHeight).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.BorderWidth).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.BorderRadius).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.FontSize).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.LetterSpacing).SetValidator(new NullableCssLengthValidator());
        RuleFor(style => style.Margin).SetValidator(new LogicalSpacingOverrideValidator()!);
        RuleFor(style => style.Padding).SetValidator(new LogicalSpacingOverrideValidator()!);
    }

    private static bool BeValidColor(CssColor? color) =>
        color is null || CssColorValidator.IsValid(color.Value);
}

public static class CssColorValidator
{
    private static readonly Regex RgbaPattern = new(
        @"^rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(0(?:\.\d+)?|1(?:\.0+)?)\s*\)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool IsValid(CssColor color)
    {
        var value = color.Value;
        if (value.Length is 4 or 7 or 9 &&
            value[0] == '#' &&
            value.AsSpan(1).IndexOfAnyExcept(
                "0123456789abcdefABCDEF".AsSpan()) < 0)
        {
            return true;
        }

        var match = RgbaPattern.Match(value);
        return match.Success &&
               int.TryParse(match.Groups[1].Value, out var red) && red <= 255 &&
               int.TryParse(match.Groups[2].Value, out var green) && green <= 255 &&
               int.TryParse(match.Groups[3].Value, out var blue) && blue <= 255 &&
               decimal.TryParse(
                   match.Groups[4].Value,
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out var alpha) &&
               alpha is >= 0m and <= 1m;
    }
}

internal sealed class LinearGradientValidator : AbstractValidator<LinearGradient>
{
    public LinearGradientValidator()
    {
        RuleFor(gradient => gradient.Type).IsInEnum();
        RuleFor(gradient => gradient.Angle).InclusiveBetween(0m, 360m);
        RuleFor(gradient => gradient.StartColor).Must(CssColorValidator.IsValid);
        RuleFor(gradient => gradient.EndColor).Must(CssColorValidator.IsValid);
        RuleFor(gradient => gradient.StartPosition).InclusiveBetween(0m, 100m);
        RuleFor(gradient => gradient.EndPosition).InclusiveBetween(0m, 100m);
        RuleFor(gradient => gradient.EndPosition)
            .GreaterThanOrEqualTo(gradient => gradient.StartPosition);
        RuleFor(gradient => gradient.RadialShape).IsInEnum();
        RuleFor(gradient => gradient.RadialPosition).IsInEnum();
    }
}

internal sealed class BoxShadowValidator : AbstractValidator<BoxShadow>
{
    public BoxShadowValidator()
    {
        RuleFor(shadow => shadow.OffsetX).InclusiveBetween(-200m, 200m);
        RuleFor(shadow => shadow.OffsetY).InclusiveBetween(-200m, 200m);
        RuleFor(shadow => shadow.Blur).InclusiveBetween(0m, 200m);
        RuleFor(shadow => shadow.Spread).InclusiveBetween(-100m, 100m);
        RuleFor(shadow => shadow.Color).Must(CssColorValidator.IsValid);
    }
}

internal sealed class BackgroundImageStyleValidator :
    AbstractValidator<BackgroundImageStyle>
{
    public BackgroundImageStyleValidator()
    {
        RuleFor(image => image.MediaId).GreaterThanOrEqualTo(0);
        RuleFor(image => image.Url)
            .MaximumLength(2048)
            .NotEmpty()
            .When(image => image.Enabled);
        RuleFor(image => image.Size).IsInEnum();
        RuleFor(image => image.Repeat).IsInEnum();
        RuleFor(image => image.Position).IsInEnum();
    }
}

internal sealed class NullableCssLengthValidator : AbstractValidator<CssLength?>
{
    public NullableCssLengthValidator()
    {
        RuleFor(length => length!.Value)
            .SetValidator(new CssLengthValidator())
            .When(length => length is not null);
    }
}

internal sealed class LogicalSpacingValidator : AbstractValidator<LogicalSpacing>
{
    public LogicalSpacingValidator()
    {
        RuleFor(spacing => spacing.BlockStart).SetValidator(new NullableCssLengthValidator());
        RuleFor(spacing => spacing.BlockEnd).SetValidator(new NullableCssLengthValidator());
        RuleFor(spacing => spacing.InlineStart).SetValidator(new NullableCssLengthValidator());
        RuleFor(spacing => spacing.InlineEnd).SetValidator(new NullableCssLengthValidator());
    }
}

internal sealed class LogicalSpacingOverrideValidator : AbstractValidator<LogicalSpacingOverride>
{
    public LogicalSpacingOverrideValidator()
    {
        RuleFor(spacing => spacing.BlockStart).SetValidator(new NullableCssLengthValidator());
        RuleFor(spacing => spacing.BlockEnd).SetValidator(new NullableCssLengthValidator());
        RuleFor(spacing => spacing.InlineStart).SetValidator(new NullableCssLengthValidator());
        RuleFor(spacing => spacing.InlineEnd).SetValidator(new NullableCssLengthValidator());
    }
}
