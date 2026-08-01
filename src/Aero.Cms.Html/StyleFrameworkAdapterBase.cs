namespace Aero.Cms.Html;

/// <summary>
/// Shared exact-mapping mechanics for built-in utility-framework adapters.
/// </summary>
public abstract class StyleFrameworkAdapterBase : IStyleFrameworkAdapter
{
    /// <inheritdoc />
    public abstract string AdapterId { get; }

    /// <inheritdoc />
    public abstract string AdapterVersion { get; }

    /// <inheritdoc />
    public FrameworkStyleMapping Map(HtmlStyle style, IStyleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(profile);

        var residual = HtmlTreeOperations.CloneStyle(style)!;
        var classes = new List<string>();
        MapLayout(residual, classes);
        MapSpacing(residual, classes);
        MapSizing(residual, classes);
        MapSurface(residual, classes);
        MapTypography(residual, classes);
        PruneEmptyGroups(residual);

        return new FrameworkStyleMapping(
            classes.Distinct(StringComparer.Ordinal).ToArray(),
            IsEmpty(residual) ? null : residual);
    }

    /// <summary>Consumes layout values that have exact framework utility equivalents.</summary>
    protected abstract void MapLayout(HtmlStyle residual, ICollection<string> classes);

    /// <summary>Consumes spacing values that have exact framework utility equivalents.</summary>
    protected abstract void MapSpacing(HtmlStyle residual, ICollection<string> classes);

    /// <summary>Consumes sizing values that have exact framework utility equivalents.</summary>
    protected abstract void MapSizing(HtmlStyle residual, ICollection<string> classes);

    /// <summary>Consumes surface values that have exact framework utility equivalents.</summary>
    protected abstract void MapSurface(HtmlStyle residual, ICollection<string> classes);

    /// <summary>Consumes typography values that have exact framework utility equivalents.</summary>
    protected abstract void MapTypography(HtmlStyle residual, ICollection<string> classes);

    /// <summary>
    /// Maps a spacing group only when all four logical sides are equal and the framework supports the value exactly.
    /// </summary>
    protected static bool TryMapUniformSpacing(
        CssLogicalSpacing? spacing,
        Func<CssLength, string?> classFactory,
        ICollection<string> classes)
    {
        if (spacing?.BlockStart is null
            || spacing.InlineEnd is null
            || spacing.BlockEnd is null
            || spacing.InlineStart is null
            || !SameLength(spacing.BlockStart, spacing.InlineEnd)
            || !SameLength(spacing.BlockStart, spacing.BlockEnd)
            || !SameLength(spacing.BlockStart, spacing.InlineStart))
        {
            return false;
        }

        var className = classFactory(spacing.BlockStart);
        if (className is null)
        {
            return false;
        }

        classes.Add(className);
        return true;
    }

    /// <summary>Compares both the numeric value and unit of two constrained lengths.</summary>
    protected static bool SameLength(CssLength left, CssLength right) =>
        left.Unit == right.Unit && left.Value == right.Value;

    /// <summary>Removes empty nested style groups so a fully consumed residual becomes <see langword="null"/>.</summary>
    protected static void PruneEmptyGroups(HtmlStyle style)
    {
        if (style.Surface is { } surface
            && surface.BackgroundColor is null
            && string.IsNullOrWhiteSpace(surface.BackgroundImageUrl)
            && surface.OverlayColor is null
            && surface.OverlayOpacity is null
            && surface.BackgroundFit is null
            && surface.BackgroundPosition is null
            && surface.BackgroundRepeat is null
            && surface.BorderRadius is null)
        {
            style.Surface = null;
        }

        if (style.Typography is { } typography
            && typography.Color is null
            && typography.FontSize is null
            && typography.FontWeight is null
            && typography.LineHeight is null
            && typography.LetterSpacing is null
            && typography.Alignment is null
            && typography.Gradient is null)
        {
            style.Typography = null;
        }
    }

    /// <summary>Determines whether any framework-neutral style intent remains.</summary>
    protected static bool IsEmpty(HtmlStyle style) =>
        style.Display is null
        && style.FlexDirection is null
        && style.GridColumns is null
        && !style.StackOnSmallScreens
        && style.Gap is null
        && style.AlignItems is null
        && style.JustifyContent is null
        && style.Padding is null
        && style.Margin is null
        && style.MinimumHeight is null
        && style.Surface is null
        && style.Typography is null;
}
