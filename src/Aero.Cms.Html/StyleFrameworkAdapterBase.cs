namespace Aero.Cms.Html;

/// <summary>
/// Shared exact-mapping mechanics for built-in utility-framework adapters.
/// </summary>
public abstract class StyleFrameworkAdapterBase : IStyleFrameworkAdapter
{
    public abstract string AdapterId { get; }
    public abstract string AdapterVersion { get; }

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

    protected abstract void MapLayout(HtmlStyle residual, ICollection<string> classes);
    protected abstract void MapSpacing(HtmlStyle residual, ICollection<string> classes);
    protected abstract void MapSizing(HtmlStyle residual, ICollection<string> classes);
    protected abstract void MapSurface(HtmlStyle residual, ICollection<string> classes);
    protected abstract void MapTypography(HtmlStyle residual, ICollection<string> classes);

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

    protected static bool SameLength(CssLength left, CssLength right) =>
        left.Unit == right.Unit && left.Value == right.Value;

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
