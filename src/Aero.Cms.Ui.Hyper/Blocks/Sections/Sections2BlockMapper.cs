using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Sections;

public static class Sections2BlockMapper
{
    public static NeoPageNode ToNode(Sections2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.sections.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl)
        }
    };

    public static Sections2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1731690415686-e68f78e2b5bd?auto=format&fit=crop&q=80&w=1160")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
