using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

public static class Cta2BlockMapper
{
    public static NeoPageNode ToNode(Cta2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.ctas.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["buttonText"] = JsonSerializer.SerializeToElement(block.ButtonText),
            ["placeholder"] = JsonSerializer.SerializeToElement(block.Placeholder),
            ["formAction"] = JsonSerializer.SerializeToElement(block.FormAction)
        }
    };

    public static Cta2Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
        ButtonText = GetString(node, "buttonText", "Sign Up"),
        Placeholder = GetString(node, "placeholder", "Email address"),
        FormAction = GetString(node, "formAction", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
