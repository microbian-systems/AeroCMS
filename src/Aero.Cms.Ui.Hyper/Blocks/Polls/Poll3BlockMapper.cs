using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

/// <summary>
/// Represents a class for Poll3BlockMapper.
/// </summary>
public static class Poll3BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Poll3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.polls.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["question"] = JsonSerializer.SerializeToElement(block.Question),
            ["pollName"] = JsonSerializer.SerializeToElement(block.PollName),
            ["maxRating"] = JsonSerializer.SerializeToElement(block.MaxRating)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Poll3Block FromNode(NeoPageNode node) => new()
    {
        Question = GetString(node, "question", "Leave a rating"),
        PollName = GetString(node, "pollName", "Rating1"),
        MaxRating = GetInt(node, "maxRating", 5)
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static int GetInt(NeoPageNode node, string key, int fallback) =>
        node.Properties.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : fallback;
}
