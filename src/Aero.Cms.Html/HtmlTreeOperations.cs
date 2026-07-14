using Aero.Core;

namespace Aero.Cms.Html;

/// <summary>
/// Provides identity-safe tree operations for page editing and publication snapshots.
/// </summary>
public static class HtmlTreeOperations
{
    /// <summary>
    /// Produces a structural copy of page content that preserves stable editor identities.
    /// </summary>
    public static HtmlPageContent ClonePreservingNodeIds(HtmlPageContent source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new HtmlPageContent
        {
            Root = ClonePreservingNodeIds(source.Root)
        };
    }

    /// <summary>
    /// Produces a structural copy that preserves stable editor identities.
    /// Used when publishing a draft snapshot; template insertion should use
    /// <see cref="CloneWithFreshNodeIds"/> instead.
    /// </summary>
    public static HtmlNode ClonePreservingNodeIds(HtmlNode source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new HtmlNode
        {
            NodeId = source.NodeId,
            Kind = source.Kind,
            TagName = source.TagName,
            Text = source.Text,
            Attributes = new Dictionary<string, string>(source.Attributes, StringComparer.Ordinal),
            ThemeClasses = [.. source.ThemeClasses],
            Style = CloneStyle(source.Style),
            Children = source.Children.Select(ClonePreservingNodeIds).ToList()
        };
    }

    /// <summary>
    /// Produces a structural copy with fresh editor identities for every node.
    /// </summary>
    public static HtmlNode CloneWithFreshNodeIds(HtmlNode source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new HtmlNode
        {
            NodeId = Snowflake.NewId(),
            Kind = source.Kind,
            TagName = source.TagName,
            Text = source.Text,
            Attributes = new Dictionary<string, string>(source.Attributes, StringComparer.Ordinal),
            ThemeClasses = [.. source.ThemeClasses],
            Style = CloneStyle(source.Style),
            Children = source.Children.Select(CloneWithFreshNodeIds).ToList()
        };
    }

    /// <summary>
    /// Finds the node with the requested editor identity in depth-first order.
    /// </summary>
    public static HtmlNode? FindById(HtmlNode root, long nodeId)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.NodeId == nodeId)
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var found = FindById(child, nodeId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the direct parent of the requested node identity, or <see langword="null"/> for the root or a missing node.
    /// </summary>
    public static HtmlNode? FindParentById(HtmlNode root, long nodeId)
    {
        ArgumentNullException.ThrowIfNull(root);

        foreach (var child in root.Children)
        {
            if (child.NodeId == nodeId)
            {
                return root;
            }

            var parent = FindParentById(child, nodeId);
            if (parent is not null)
            {
                return parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether a subtree contains unique editor identities.
    /// </summary>
    public static bool HasUniqueNodeIds(HtmlNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var seen = new HashSet<long>();
        return HasUniqueNodeIds(root, seen);
    }

    private static bool HasUniqueNodeIds(HtmlNode node, ISet<long> seen)
    {
        if (!seen.Add(node.NodeId))
        {
            return false;
        }

        return node.Children.All(child => HasUniqueNodeIds(child, seen));
    }

    /// <summary>
    /// Creates an independent copy of constrained style intent.
    /// </summary>
    public static HtmlStyle? CloneStyle(HtmlStyle? source) => source is null
        ? null
        : new HtmlStyle
        {
            Display = source.Display,
            FlexDirection = source.FlexDirection,
            GridColumns = source.GridColumns,
            StackOnSmallScreens = source.StackOnSmallScreens,
            Gap = CloneLength(source.Gap),
            AlignItems = source.AlignItems,
            JustifyContent = source.JustifyContent,
            Padding = CloneSpacing(source.Padding),
            Margin = CloneSpacing(source.Margin),
            MinimumHeight = CloneLength(source.MinimumHeight),
            Surface = CloneSurface(source.Surface),
            Typography = CloneTypography(source.Typography)
        };

    private static CssTypographyStyle? CloneTypography(CssTypographyStyle? source) => source is null
        ? null
        : new CssTypographyStyle
        {
            Color = CloneColor(source.Color),
            FontSize = CloneLength(source.FontSize),
            FontWeight = source.FontWeight,
            LineHeight = source.LineHeight,
            LetterSpacing = CloneLength(source.LetterSpacing),
            Alignment = source.Alignment,
            Gradient = CloneGradient(source.Gradient)
        };

    private static CssTextGradient? CloneGradient(CssTextGradient? source) => source is null
        ? null
        : new CssTextGradient
        {
            StartColor = CloneColor(source.StartColor)!,
            EndColor = CloneColor(source.EndColor)!,
            AngleDegrees = source.AngleDegrees
        };

    private static CssSurfaceStyle? CloneSurface(CssSurfaceStyle? source) => source is null
        ? null
        : new CssSurfaceStyle
        {
            BackgroundColor = CloneColor(source.BackgroundColor),
            BackgroundImageUrl = source.BackgroundImageUrl,
            OverlayColor = CloneColor(source.OverlayColor),
            OverlayOpacity = source.OverlayOpacity,
            BackgroundFit = source.BackgroundFit,
            BackgroundPosition = source.BackgroundPosition,
            BackgroundRepeat = source.BackgroundRepeat,
            BorderRadius = CloneLength(source.BorderRadius)
        };

    private static CssColor? CloneColor(CssColor? source) => source is null
        ? null
        : new CssColor { Kind = source.Kind, Value = source.Value };

    private static CssLogicalSpacing? CloneSpacing(CssLogicalSpacing? source) => source is null
        ? null
        : new CssLogicalSpacing
        {
            BlockStart = CloneLength(source.BlockStart),
            InlineEnd = CloneLength(source.InlineEnd),
            BlockEnd = CloneLength(source.BlockEnd),
            InlineStart = CloneLength(source.InlineStart)
        };

    private static CssLength? CloneLength(CssLength? source) => source is null
        ? null
        : new CssLength { Value = source.Value, Unit = source.Unit };
}
