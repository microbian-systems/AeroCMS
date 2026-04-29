using System.Text.Json;
using Scriban.Runtime;

namespace Aero.Cms.Core.Blocks.Dynamic;

public static class JsonToScribanMapper
{
    public static ScriptObject CreateGlobals(JsonDocument? data, int maxDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var globals = new ScriptObject
        {
            ["block"] = data is null
                ? new ScriptObject()
                : ConvertElement(data.RootElement, 0, maxDepth)
        };

        return globals;
    }

    private static object? ConvertElement(JsonElement element, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            throw new InvalidOperationException($"Dynamic block data exceeds the maximum depth of {maxDepth}.");
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element, depth, maxDepth),
            JsonValueKind.Array => ConvertArray(element, depth, maxDepth),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ConvertNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => null
        };
    }

    private static ScriptObject ConvertObject(JsonElement element, int depth, int maxDepth)
    {
        var scriptObject = new ScriptObject();

        foreach (var property in element.EnumerateObject())
        {
            scriptObject[property.Name] = ConvertElement(property.Value, depth + 1, maxDepth);
        }

        return scriptObject;
    }

    private static ScriptArray ConvertArray(JsonElement element, int depth, int maxDepth)
    {
        var scriptArray = new ScriptArray();

        foreach (var item in element.EnumerateArray())
        {
            scriptArray.Add(ConvertElement(item, depth + 1, maxDepth));
        }

        return scriptArray;
    }

    private static object ConvertNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (element.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return element.GetDouble();
    }
}
