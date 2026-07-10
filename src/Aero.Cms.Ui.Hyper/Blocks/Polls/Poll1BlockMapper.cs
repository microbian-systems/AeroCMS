using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

/// <summary>
/// Represents a class for Poll1BlockMapper.
/// </summary>
public static class Poll1BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Poll1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.polls.1",
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

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Poll1Block FromNode(NeoPageNode node) => new()
    {
        Question = GetString(node, "question", "Where should we go for lunch?"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit, amet consectetur adipisicing elit."),
        EndDate = GetString(node, "endDate", "October 31, 2025"),
        EndDateIso = GetString(node, "endDateIso", "2025-10-31"),
        Options = node.Properties.TryGetValue("options", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Poll1Option>>(element.GetRawText()) ?? Poll1Block.DefaultOptions.Select(CloneOption).ToList()
            : Poll1Block.DefaultOptions.Select(CloneOption).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Poll1Option CloneOption(Poll1Option option) => new()
    {
        Label = option.Label,
        Percentage = option.Percentage
    };
}
