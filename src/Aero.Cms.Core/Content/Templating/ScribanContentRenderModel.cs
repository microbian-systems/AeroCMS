using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Immutable, explicitly projected input for a content-type Scriban template.
/// No persisted document or application service is exposed to the template.
/// </summary>
/// <param name="Fields">The cloned JSON object exposed as the root <c>fields</c> scope.</param>
/// <param name="Item">The metadata exposed as the <c>item</c> scope.</param>
/// <param name="ContentType">The metadata exposed as the <c>content_type</c> scope.</param>
/// <param name="Site">The metadata exposed as the <c>site</c> scope.</param>
/// <param name="References">Resolved, bounded virtual entries exposed as the <c>references</c> scope.</param>
public sealed record ScribanContentRenderModel(
    JsonElement Fields,
    ScribanContentItemRenderScope Item,
    ScribanContentTypeRenderScope ContentType,
    ScribanSiteRenderScope Site,
    JsonElement References = default)
{
    /// <summary>
    /// Projects the current content item and its definition into the supported
    /// template scopes.
    /// </summary>
    /// <param name="contentType">The content type metadata to project.</param>
    /// <param name="item">The content item to project.</param>
    /// <param name="site">
    /// Optional site metadata. When omitted, only the item's site identifier and culture are populated.
    /// </param>
    /// <param name="references">Optional bounded virtual-reference projections for the template.</param>
    /// <returns>A detached render model with cloned, ordinally ordered JSON objects.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contentType"/> or <paramref name="item"/> is null.</exception>
    public static ScribanContentRenderModel Create(
        ContentTypeDefinition contentType,
        ContentItem item,
        ScribanSiteRenderScope? site = null,
        JsonElement? references = null)
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
                BaseUrl: null),
            references ?? CreateJsonObject([]));
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
/// <param name="Id">The content-item identifier.</param>
/// <param name="Slug">The content-item slug.</param>
/// <param name="Title">The optional title.</param>
/// <param name="Culture">The content culture.</param>
/// <param name="PublicationState">The publication-state name.</param>
/// <param name="Version">The content version number.</param>
/// <param name="CreatedOn">The invariant round-trip creation timestamp.</param>
/// <param name="ModifiedOn">The invariant round-trip modification timestamp, if present.</param>
/// <param name="PublishedOn">The invariant round-trip publication timestamp, if present.</param>
/// <param name="Fields">A cloned JSON object containing the item's fields.</param>
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
/// <param name="Id">The content-type identifier.</param>
/// <param name="Alias">The content-type alias.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The optional description.</param>
/// <param name="Category">The optional category.</param>
/// <param name="Fields">The projected field definitions.</param>
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
/// <param name="Name">The field name.</param>
/// <param name="FieldType">The field type identifier.</param>
/// <param name="Label">The optional display label.</param>
/// <param name="Required">Whether the definition marks the field as required.</param>
/// <param name="DefaultValue">The optional default value.</param>
/// <param name="Placeholder">The optional editor placeholder.</param>
/// <param name="Settings">A cloned JSON object containing field settings.</param>
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
/// <param name="Id">The site identifier.</param>
/// <param name="CurrentCulture">The current content culture.</param>
/// <param name="Name">The optional site name.</param>
/// <param name="DefaultCulture">The optional default site culture.</param>
/// <param name="BaseUrl">The optional site base URL.</param>
public sealed record ScribanSiteRenderScope(
    long Id,
    string CurrentCulture,
    string? Name,
    string? DefaultCulture,
    string? BaseUrl);
