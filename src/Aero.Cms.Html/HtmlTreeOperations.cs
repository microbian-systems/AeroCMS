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
    /// <param name="source">The content to copy.</param>
    /// <returns>A fully independent page tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
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
    /// <param name="source">The subtree to copy.</param>
    /// <returns>A fully independent subtree with the same node identities.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
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
    /// <param name="source">The subtree to copy.</param>
    /// <returns>A fully independent subtree suitable for insertion alongside the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
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
    /// <param name="root">The root of the search subtree.</param>
    /// <param name="nodeId">The stable identity to locate.</param>
    /// <returns>The first matching node reference, or <see langword="null"/> when absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
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
    /// <param name="root">The root of the search subtree.</param>
    /// <param name="nodeId">The stable identity whose parent is required.</param>
    /// <returns>The direct parent reference, or <see langword="null"/> for the root or an absent identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
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
    /// <param name="root">The subtree to inspect.</param>
    /// <returns><see langword="true"/> when every encountered identity is distinct.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    public static bool HasUniqueNodeIds(HtmlNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var seen = new HashSet<long>();
        return HasUniqueNodeIds(root, seen);
    }

    /// <summary>Performs the recursive identity check using traversal-wide state.</summary>
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
    /// <param name="source">The style to copy, or <see langword="null"/>.</param>
    /// <returns>An independent deep copy, or <see langword="null"/> when no style was supplied.</returns>
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

    /// <summary>Clones typography and nested color/gradient values.</summary>
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

    /// <summary>Clones both color stops and the gradient angle.</summary>
    private static CssTextGradient? CloneGradient(CssTextGradient? source) => source is null
        ? null
        : new CssTextGradient
        {
            StartColor = CloneColor(source.StartColor)!,
            EndColor = CloneColor(source.EndColor)!,
            AngleDegrees = source.AngleDegrees
        };

    /// <summary>Clones surface colors, sizing values, and immutable scalar settings.</summary>
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

    /// <summary>Clones a literal or token color reference.</summary>
    private static CssColor? CloneColor(CssColor? source) => source is null
        ? null
        : new CssColor { Kind = source.Kind, Value = source.Value };

    /// <summary>Clones every populated logical side.</summary>
    private static CssLogicalSpacing? CloneSpacing(CssLogicalSpacing? source) => source is null
        ? null
        : new CssLogicalSpacing
        {
            BlockStart = CloneLength(source.BlockStart),
            InlineEnd = CloneLength(source.InlineEnd),
            BlockEnd = CloneLength(source.BlockEnd),
            InlineStart = CloneLength(source.InlineStart)
        };

    /// <summary>Clones a constrained numeric length.</summary>
    private static CssLength? CloneLength(CssLength? source) => source is null
        ? null
        : new CssLength { Value = source.Value, Unit = source.Unit };
}
