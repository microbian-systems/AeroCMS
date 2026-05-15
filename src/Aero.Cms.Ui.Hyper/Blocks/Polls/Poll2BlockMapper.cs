using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

public static class Poll2BlockMapper
{
    public static NeoPageNode ToNode(Poll2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.polls.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["question"] = JsonSerializer.SerializeToElement(block.Question),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["endDate"] = JsonSerializer.SerializeToElement(block.EndDate),
            ["endDateIso"] = JsonSerializer.SerializeToElement(block.EndDateIso),
            ["options"] = JsonSerializer.SerializeToElement(block.Options)
        }
    };

    public static Poll2Block FromNode(NeoPageNode node) => new()
    {
        Question = GetString(node, "question", "Where should we go for lunch?"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit, amet consectetur adipisicing elit."),
        EndDate = GetString(node, "endDate", "October 31, 2025"),
        EndDateIso = GetString(node, "endDateIso", "2025-10-31"),
        Options = node.Properties.TryGetValue("options", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Poll2Option>>(element.GetRawText()) ?? Poll2Block.DefaultOptions.Select(CloneOption).ToList()
            : Poll2Block.DefaultOptions.Select(CloneOption).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Poll2Option CloneOption(Poll2Option option) => new()
    {
        Label = option.Label,
        Percentage = option.Percentage
    };
}
