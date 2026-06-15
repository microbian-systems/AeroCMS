namespace Aero.Cms.Abstractions.Blocks.Neo;

using System.Text.Json;

/// <summary>
/// Builds bounded, literal Tailwind classes for composed Neo containers.
/// </summary>
public static class NeoContainerLayoutCss
{
    public static string FromProperties(IReadOnlyDictionary<string, JsonElement> properties)
    {
        var layout = GetString(properties, "layout", "stack");
        var gap = GetInt(properties, "gap", 4);

        return layout switch
        {
            "grid" => $"w-full {GridColumns(GetInt(properties, "columns", 1))} {Gap(gap)}",
            _ => $"w-full flex flex-col {Gap(gap)}"
        };
    }

    private static string GridColumns(int columns) => columns switch
    {
        2 => "grid grid-cols-2",
        3 => "grid grid-cols-3",
        4 => "grid grid-cols-4",
        12 => "grid grid-cols-12",
        _ => "grid grid-cols-1"
    };

    private static string Gap(int gap) => gap switch
    {
        0 => "gap-0",
        2 => "gap-2",
        6 => "gap-6",
        8 => "gap-8",
        _ => "gap-4"
    };

    private static string GetString(IReadOnlyDictionary<string, JsonElement> properties, string name, string fallback) =>
        properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static int GetInt(IReadOnlyDictionary<string, JsonElement> properties, string name, int fallback) =>
        properties.TryGetValue(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : fallback;
}
