using System.Text.Json;
using Scriban.Runtime;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Projects JSON and render metadata into the named Scriban global scopes.
/// </summary>
public static class JsonToScribanMapper
{
    /// <summary>Converts one JSON value into Scriban's closed dynamic value model.</summary>
    public static object? Convert(JsonElement element, int maxDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);
        return ConvertElement(element, 0, maxDepth);
    }

    /// <summary>
    /// Creates the <c>fields</c>, <c>item</c>, <c>content_type</c>, <c>site</c>, and
    /// resolved <c>references</c>
    /// scopes and adds explicitly trusted imports.
    /// </summary>
    /// <param name="model">The projected render model.</param>
    /// <param name="maxDepth">The maximum JSON nesting depth to convert.</param>
    /// <param name="imports">
    /// Additional application-supplied scopes. Import names must be nonblank and must not
    /// conflict with a built-in scope.
    /// </param>
    /// <returns>A globals object whose top-level scope bindings are read-only.</returns>
    /// <remarks>
    /// Imported <see cref="ScriptObject"/> instances are deep-cloned before exposure. They
    /// remain a trust boundary because any functions or values placed in them originate with
    /// the caller. JSON integers are projected as <see cref="long"/>, other representable
    /// numbers as <see cref="decimal"/>, and remaining numbers as <see cref="double"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or an import value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDepth"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// An import name is blank or reserved, or JSON input exceeds <paramref name="maxDepth"/>.
    /// </exception>
    public static ScriptObject CreateGlobals(
    ScribanContentRenderModel model,
    int maxDepth,
    IReadOnlyDictionary<string, ScriptObject>? imports = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var globals = new ScriptObject
        {
            ["fields"] = Convert(model.Fields, maxDepth),
            ["item"] = CreateItemScope(model.Item, maxDepth),
            ["content_type"] = CreateContentTypeScope(model.ContentType, maxDepth),
            ["site"] = CreateSiteScope(model.Site),
            ["references"] = model.References.ValueKind == JsonValueKind.Object
                ? Convert(model.References, maxDepth)
                : new ScriptObject()
        };

        SetReadOnly(globals, "fields");
        SetReadOnly(globals, "item");
        SetReadOnly(globals, "content_type");
        SetReadOnly(globals, "site");
        SetReadOnly(globals, "references");

        if (imports is not null)
        {
            foreach (var (name, value) in imports)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new InvalidOperationException("Scriban import names cannot be empty.");
                }

                if (globals.Contains(name))
                {
                    throw new InvalidOperationException(
                        $"Scriban import name '{name}' conflicts with a reserved content scope.");
                }

                ArgumentNullException.ThrowIfNull(value);
                globals[name] = value.Clone(deep: true);
                SetReadOnly(globals, name);
            }
        }

        return globals;
    }

    private static ScriptObject CreateItemScope(
        ScribanContentItemRenderScope item,
        int maxDepth) =>
        new()
        {
            ["id"] = item.Id,
            ["slug"] = item.Slug,
            ["title"] = item.Title,
            ["culture"] = item.Culture,
            ["publication_state"] = item.PublicationState,
            ["version"] = item.Version,
            ["created_on"] = item.CreatedOn,
            ["modified_on"] = item.ModifiedOn,
            ["published_on"] = item.PublishedOn,
            ["fields"] = ConvertElement(item.Fields, 0, maxDepth)
        };

    private static ScriptObject CreateContentTypeScope(
        ScribanContentTypeRenderScope contentType,
        int maxDepth)
    {
        var fields = new ScriptArray();
        foreach (var field in contentType.Fields)
        {
            fields.Add(new ScriptObject
            {
                ["name"] = field.Name,
                ["field_type"] = field.FieldType,
                ["label"] = field.Label,
                ["required"] = field.Required,
                ["default_value"] = field.DefaultValue,
                ["placeholder"] = field.Placeholder,
                ["settings"] = Convert(field.Settings, maxDepth)
            });
        }

        return new ScriptObject
        {
            ["id"] = contentType.Id,
            ["alias"] = contentType.Alias,
            ["name"] = contentType.Name,
            ["description"] = contentType.Description,
            ["category"] = contentType.Category,
            ["fields"] = fields
        };
    }

    private static ScriptObject CreateSiteScope(ScribanSiteRenderScope site) =>
        new()
        {
            ["id"] = site.Id,
            ["current_culture"] = site.CurrentCulture,
            ["name"] = site.Name,
            ["default_culture"] = site.DefaultCulture,
            ["base_url"] = site.BaseUrl
        };

    private static void SetReadOnly(ScriptObject globals, string name) =>
        globals.SetReadOnly(name, readOnly: true);

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
            scriptObject.SetReadOnly(property.Name, readOnly: true);
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
