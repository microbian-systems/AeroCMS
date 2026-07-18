using System.Text.Json.Serialization.Metadata;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Represents a class for ContentItemExtensions.
/// </summary>
public static class ContentItemExtensions
{
        /// <summary>
    /// Get method.
    /// </summary>
public static T? Get<T>(this ContentItem item, string field, JsonTypeInfo<T> typeInfo)
    {
        if (!item.Fields.TryGetValue(field, out var element))
            return default;
        return JsonSerializer.Deserialize(element.GetRawText(), typeInfo);
    }
}
