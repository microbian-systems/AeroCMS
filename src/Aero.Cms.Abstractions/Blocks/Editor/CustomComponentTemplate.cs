using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Captures reusable component templates and creates isolated editor instances.
/// </summary>
public static class CustomComponentTemplate
{
    public static NeoPageNode Capture(NeoPageNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return EditorNodeMemento.Capture(root).Restore();
    }

    public static NeoPageNode CreateInstance(NeoPageNode root)
    {
        var instance = Capture(root);
        AssignFreshIds(instance);
        return instance;
    }

    public static IReadOnlyList<string> GetReferencedCatalogIds(NeoPageNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCatalogIds(root, ids);
        return ids.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddCatalogIds(NeoPageNode node, ISet<string> ids)
    {
        if (!string.IsNullOrWhiteSpace(node.CatalogId))
        {
            ids.Add(node.CatalogId);
        }

        foreach (var child in node.Children)
        {
            AddCatalogIds(child, ids);
        }
    }

    private static void AssignFreshIds(NeoPageNode node)
    {
        node.NodeId = Guid.NewGuid().ToString("N");
        foreach (var child in node.Children)
        {
            AssignFreshIds(child);
        }
    }
}
