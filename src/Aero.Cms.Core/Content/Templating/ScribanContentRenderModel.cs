using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Immutable, explicitly projected input for a content-type Scriban template.
/// No persisted document or application service is exposed to the template.
/// </summary>
public sealed record ScribanContentRenderModel(
    JsonElement Fields,
    ScribanContentItemRenderScope Item,
    ScribanContentTypeRenderScope ContentType,
    ScribanSiteRenderScope Site)
{
    /// <summary>
    /// Projects the current content item and its definition into the supported
    /// template scopes.
    /// </summary>
    public static ScribanContentRenderModel Create(
        ContentTypeDefinition contentType,
        ContentItem item,
        ScribanSiteRenderScope? site = null)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(item);

        var fields = CreateJsonObject(item.Fields);
        var itemScope = new ScribanContentItemRenderScope(
            item.Id,
            item.Slug,
            item.Title,
            item.Culture,
            item.PublicationState.ToString(),
            item.VersionNumber,
            FormatDate(item.CreatedOn),
            FormatDate(item.ModifiedOn),
            FormatDate(item.PublishedOn),
            fields);
        var typeScope = new ScribanContentTypeRenderScope(
            contentType.Id,
            contentType.Alias,
            contentType.Name,
            contentType.Description,
            contentType.Category,
            contentType.Fields
                .Select(static field => new ScribanContentFieldRenderScope(
                    field.Name,
                    field.FieldType,
                    field.Label,
                    field.Required,
                    field.DefaultValue,
                    field.Placeholder,
                    CreateJsonObject(field.Settings)))
                .ToImmutableArray());

        return new ScribanContentRenderModel(
            fields,
            itemScope,
            typeScope,
            site ?? new ScribanSiteRenderScope(
                item.SiteId,
                item.Culture,
                Name: null,
                DefaultCulture: null,
                BaseUrl: null));
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static string? FormatDate(DateTimeOffset? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture);

    private static JsonElement CreateJsonObject(
        IEnumerable<KeyValuePair<string, JsonElement>> properties)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in properties.OrderBy(
                         static property => property.Key,
                         StringComparer.Ordinal))
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}

/// <summary>
/// Content-item metadata available to Scriban as <c>item</c>.
/// </summary>
public sealed record ScribanContentItemRenderScope(
    long Id,
    string Slug,
    string? Title,
    string Culture,
    string PublicationState,
    int Version,
    string CreatedOn,
    string? ModifiedOn,
    string? PublishedOn,
    JsonElement Fields);

/// <summary>
/// Content-type metadata available to Scriban as <c>content_type</c>.
/// </summary>
public sealed record ScribanContentTypeRenderScope(
    long Id,
    string Alias,
    string Name,
    string? Description,
    string? Category,
    ImmutableArray<ScribanContentFieldRenderScope> Fields);

/// <summary>
/// Field-definition metadata exposed inside <c>content_type.fields</c>.
/// </summary>
public sealed record ScribanContentFieldRenderScope(
    string Name,
    string FieldType,
    string? Label,
    bool Required,
    string? DefaultValue,
    string? Placeholder,
    JsonElement Settings);

/// <summary>
/// Site metadata available to Scriban as <c>site</c>.
/// </summary>
public sealed record ScribanSiteRenderScope(
    long Id,
    string CurrentCulture,
    string? Name,
    string? DefaultCulture,
    string? BaseUrl);
