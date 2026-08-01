using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Pages.Composition;
using Scriban.Runtime;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>Projects eager page content queries into a closed Scriban scope.</summary>
public static class ContentQueryToScribanMapper
{
    /// <summary>
    /// Creates the read-only <c>content.&lt;name&gt;</c> query namespace.
    /// </summary>
    public static ScriptObject CreateContentScope(
        PageContentQueryResolution resolution,
        int maximumJsonDepth = 10)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumJsonDepth);

        var content = new ScriptObject();
        foreach (var (name, result) in resolution.Results
                     .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            content[name] = CreateResult(result, maximumJsonDepth);
            content.SetReadOnly(name, readOnly: true);
        }

        return content;
    }

    private static ScriptObject CreateResult(
        ContentQueryResult result,
        int maximumJsonDepth)
    {
        var roots = new ScriptArray();
        foreach (var root in result.Roots)
        {
            roots.Add(CreateNode(root, maximumJsonDepth));
        }

        var scope = new ScriptObject
        {
            ["content_type"] = result.ContentTypeAlias,
            ["roots"] = roots,
            ["total_items"] = result.TotalItems,
            ["was_truncated"] = result.WasTruncated
        };
        SetReadOnly(scope, "content_type", "roots", "total_items", "was_truncated");
        return scope;
    }

    private static ScriptObject CreateNode(
        ContentNode node,
        int maximumJsonDepth)
    {
        var fields = new ScriptObject();
        foreach (var (name, value) in node.Fields
                     .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            fields[name] = JsonToScribanMapper.Convert(value, maximumJsonDepth);
            fields.SetReadOnly(name, readOnly: true);
        }

        var children = new ScriptArray();
        foreach (var child in node.Children)
        {
            children.Add(CreateNode(child, maximumJsonDepth));
        }

        var result = new ScriptObject
        {
            ["id"] = node.Id,
            ["content_type"] = node.ContentType,
            ["title"] = node.Title,
            ["slug"] = node.Slug,
            ["fields"] = fields,
            ["children"] = children
        };
        SetReadOnly(
            result,
            "id",
            "content_type",
            "title",
            "slug",
            "fields",
            "children");
        return result;
    }

    private static void SetReadOnly(ScriptObject value, params string[] names)
    {
        foreach (var name in names)
        {
            value.SetReadOnly(name, readOnly: true);
        }
    }
}
