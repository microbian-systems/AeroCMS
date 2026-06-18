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
    public static EditorBlock ToEditorBlock(NeoPageNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var title = GetString(node, "title");
        var summary = FirstNonEmpty(
            GetString(node, "summary"),
            GetString(node, "description"),
            GetString(node, "subText"));
        var content = FirstNonEmpty(
            GetString(node, "content"),
            GetString(node, "html"),
            GetString(node, "text"));
        var image = FirstNonEmpty(
            GetString(node, "backgroundImageUrl"),
            GetString(node, "backgroundImage"),
            GetString(node, "src"),
            GetString(node, "url"));

        return new EditorBlock
        {
            EditorId = node.NodeId,
            Type = node.CatalogId,
            Style = node.Style.DeepClone(),
            Title = title,
            MainText = title,
            SubText = summary,
            Description = summary,
            Content = content,
            BackgroundImage = image,
            Src = FirstNonEmpty(GetString(node, "src"), GetString(node, "url"), image),
            Url = GetString(node, "url"),
            Alt = GetString(node, "alt"),
            Caption = GetString(node, "caption"),
            CtaText = GetString(node, "ctaText"),
            CtaUrl = GetString(node, "ctaUrl"),
            FullWidth = GetBool(node, "fullWidth")
        };
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

    private static bool GetBool(NeoPageNode node, string name) =>
        node.Properties.TryGetValue(name, out var value) &&
        value.ValueKind == JsonValueKind.True;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
