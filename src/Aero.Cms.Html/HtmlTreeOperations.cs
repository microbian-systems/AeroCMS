using Aero.Core;

namespace Aero.Cms.Html;

/// <summary>
/// Provides identity-safe tree operations for page editing and publication snapshots.
/// </summary>
public static class HtmlTreeOperations
{
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
}
