using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Blocks.Serialization;

namespace Aero.Cms.Abstractions.Content;

public static class ContentItemExtensions
{
    public static T? Get<T>(this ContentItem item, string field)
    {
        if (!item.Fields.TryGetValue(field, out var element))
            return default;
        return JsonSerializer.Deserialize<T>(element.GetRawText(), BlockJsonContext.Default.Options);
    }

    public static T? Get<T>(this ContentItem item, string field, JsonSerializerContext context)
    {
        if (!item.Fields.TryGetValue(field, out var element))
            return default;
        return JsonSerializer.Deserialize(element.GetRawText(), typeof(T), context) is T value
            ? value
            : default;
    }
}
