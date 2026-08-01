using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Content.Templating;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentItemRendererTests
{
    [Test]
    public async Task Renders_content_type_directly_without_persisted_block_bridge()
    {
        using var source = JsonDocument.Parse("""{"title":"Aero","summary":"Fast content"}""");
        var type = new ContentTypeDefinition
        {
            Id = 101,
            SiteId = 7,
            Alias = "feature",
            Name = "Feature",
            Description = "Feature content",
            Category = "Marketing",
            ScribanTemplate =
                """
                <article>
                  <h2>{{ fields.title }}</h2>
                  <p>{{ fields.summary }}</p>
                  <small>{{ item.id }}|{{ item.slug }}|{{ item.title }}|{{ item.culture }}|{{ item.publication_state }}|{{ item.version }}</small>
                  <small>{{ content_type.id }}|{{ content_type.alias }}|{{ content_type.name }}|{{ content_type.description }}|{{ content_type.category }}</small>
                  <small>{{ content_type.fields[0].name }}|{{ content_type.fields[0].field_type }}|{{ content_type.fields[0].required }}</small>
                  <small>{{ site.id }}|{{ site.current_culture }}</small>
                </article>
                """,
            Fields =
            [
                new() { Name = "title", FieldType = "text", Required = true },
                new() { Name = "summary", FieldType = "text", Required = true }
            ]
        };
        var item = new ContentItem
        {
            Id = 202,
            SiteId = type.SiteId,
            ContentTypeAlias = type.Alias,
            Slug = "aero",
            Title = "Aero Feature",
            Culture = "en-US",
            PublicationState = Aero.Cms.Abstractions.Enums.ContentPublicationState.Published,
            VersionNumber = 4,
            Fields = new Dictionary<string, JsonElement>
            {
                ["title"] = source.RootElement.GetProperty("title").Clone(),
                ["summary"] = source.RootElement.GetProperty("summary").Clone()
            }
        };
        var renderer = new ContentItemRenderer([], new SecureScribanRenderer());

        var result = await renderer.RenderAsync(type, item);

        var ok = result as Result<string, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).Contains("<h2>Aero</h2>");
        await Assert.That(ok.Value).Contains("<p>Fast content</p>");
        await Assert.That(ok.Value).Contains("202|aero|Aero Feature|en-US|Published|4");
        await Assert.That(ok.Value).Contains("101|feature|Feature|Feature content|Marketing");
        await Assert.That(ok.Value).Contains("title|text|true");
        await Assert.That(ok.Value).Contains("7|en-US");
    }

    [Test]
    public async Task Generates_default_template_and_normalizes_non_identifier_field_names()
    {
        using var source = JsonDocument.Parse("""{"rich-text":"Hello"}""");
        var type = new ContentTypeDefinition
        {
            Id = 102,
            SiteId = 7,
            Alias = "note",
            Fields =
            [
                new() { Name = "rich-text", FieldType = "richtext", Required = true }
            ]
        };
        var item = new ContentItem
        {
            Id = 203,
            SiteId = type.SiteId,
            ContentTypeAlias = type.Alias,
            Fields = new Dictionary<string, JsonElement>
            {
                ["rich-text"] = source.RootElement.GetProperty("rich-text").Clone()
            }
        };
        var renderer = new ContentItemRenderer([], new SecureScribanRenderer());

        var result = await renderer.RenderAsync(type, item);

        var ok = result as Result<string, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).Contains("content-type-note");
        await Assert.That(ok.Value).Contains("Hello");
    }
}
