using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Transitional adapter from the page-tree storage model back to the existing
/// editor block DTO used by legacy canned block definitions and renderers.
/// </summary>
internal static class NeoPageNodeEditorBlockMapper
{
    public static EditorBlock ToEditorBlock(NeoPageNode node, EditorBlock? defaults = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        var editorBlock = defaults?.DeepClone() ?? new EditorBlock();

        editorBlock.EditorId = node.NodeId;
        editorBlock.Type = node.CatalogId;
        editorBlock.Style = node.Style.DeepClone();

        var title = FirstNonEmpty(
            GetString(node, "title"),
            GetString(node, "heading"),
            GetString(node, "text"));
        var summary = FirstNonEmpty(
            GetString(node, "summary"),
            GetString(node, "description"),
            GetString(node, "subText"),
            GetString(node, "subtitle"));
        var content = FirstNonEmpty(
            GetString(node, "content"),
            GetString(node, "html"),
            GetString(node, "markdown"),
            GetString(node, "text"));
        var image = FirstNonEmpty(
            GetString(node, "backgroundImageUrl"),
            GetString(node, "backgroundImage"),
            GetString(node, "imageUrl"),
            GetString(node, "src"),
            GetString(node, "url"));

        SetIfPresent(value => editorBlock.Title = value, title);
        SetIfPresent(value => editorBlock.MainText = value, title);
        SetIfPresent(value => editorBlock.SectionTitle = value, title);
        SetIfPresent(value => editorBlock.PageTitle = value, title);
        SetIfPresent(value => editorBlock.SubText = value, summary);
        SetIfPresent(value => editorBlock.Description = value, summary);
        SetIfPresent(value => editorBlock.PageDescription = value, summary);
        SetIfPresent(value => editorBlock.Content = value, content);
        SetIfPresent(value => editorBlock.BackgroundImage = value, image);
        SetIfPresent(value => editorBlock.Src = value, FirstNonEmpty(GetString(node, "src"), GetString(node, "url"), image));
        SetIfPresent(value => editorBlock.Url = value, FirstNonEmpty(GetString(node, "url"), GetString(node, "href")));
        SetIfPresent(value => editorBlock.Alt = value, GetString(node, "alt"));
        SetIfPresent(value => editorBlock.Caption = value, GetString(node, "caption"));
        SetIfPresent(value => editorBlock.Author = value, GetString(node, "author"));
        SetIfPresent(value => editorBlock.Eyebrow = value, GetString(node, "eyebrow"));
        SetIfPresent(value => editorBlock.Highlight = value, GetString(node, "highlight"));
        SetIfPresent(value => editorBlock.CtaText = value, FirstNonEmpty(GetString(node, "ctaText"), GetString(node, "primaryText"), GetString(node, "label")));
        SetIfPresent(value => editorBlock.CtaUrl = value, FirstNonEmpty(GetString(node, "ctaUrl"), GetString(node, "primaryUrl"), GetString(node, "href")));
        SetIfPresent(value => editorBlock.CtaText2 = value, FirstNonEmpty(GetString(node, "ctaText2"), GetString(node, "secondaryText")));
        SetIfPresent(value => editorBlock.CtaUrl2 = value, FirstNonEmpty(GetString(node, "ctaUrl2"), GetString(node, "secondaryUrl")));
        SetIfPresent(value => editorBlock.Button1Style = value, FirstNonEmpty(GetString(node, "button1Style"), GetString(node, "buttonStyle"), GetString(node, "style"), GetString(node, "variant")));
        SetIfPresent(value => editorBlock.Button2Style = value, GetString(node, "button2Style"));
        SetIfPresent(value => editorBlock.ScribanTemplate = value, FirstNonEmpty(
            GetString(node, "template"),
            GetString(node, "scribanTemplate"),
            GetString(node, "inlineTemplate"),
            GetString(node, "content")));

        SetIfPresent(value => editorBlock.ScribanDataJson = value, FirstNonEmpty(
            GetJsonOrString(node, "data"),
            GetJsonOrString(node, "json"),
            GetString(node, "scribanDataJson")));

        if (TryGetBool(node, "fullWidth", out var fullWidth))
        {
            editorBlock.FullWidth = fullWidth;
        }

        if (TryGetInt(node, "height", out var height))
        {
            editorBlock.Height = height;
        }

        if (TryGetInt(node, "columnCount", out var columnCount))
        {
            editorBlock.ColumnCount = columnCount;
        }

        if (TryGetInt(node, "rowCount", out var rowCount))
        {
            editorBlock.RowCount = rowCount;
        }

        if (TryGetInt(node, "gap", out var gap))
        {
            editorBlock.Gap = gap;
        }

        var trustMarkers = GetStringList(node, "trustMarkers", "markers");
        if (trustMarkers.Count > 0)
        {
            editorBlock.TrustMarkers = trustMarkers;
        }

        editorBlock.CompositionNodes = [node];
        return editorBlock;
    }

    private static string GetString(NeoPageNode node, string name)
    {
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => string.Empty
        };
    }

    private static bool TryGetBool(NeoPageNode node, string name, out bool result)
    {
        result = false;
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return false;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            result = value.GetBoolean();
            return true;
        }

        if (value.ValueKind == JsonValueKind.String &&
            bool.TryParse(value.GetString(), out result))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetInt(NeoPageNode node, string name, out int result)
    {
        result = 0;
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out result),
            JsonValueKind.String => int.TryParse(value.GetString(), out result),
            _ => false
        };
    }

    private static string GetJsonOrString(NeoPageNode node, string name)
    {
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static void SetIfPresent(Action<string> assign, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            assign(value);
        }
    }

    private static List<string> GetStringList(NeoPageNode node, params string[] names)
    {
        foreach (var name in names)
        {
            if (!node.Properties.TryGetValue(name, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var items = value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToList();

            if (items.Count > 0)
            {
                return items;
            }
        }

        return [];
    }
}
