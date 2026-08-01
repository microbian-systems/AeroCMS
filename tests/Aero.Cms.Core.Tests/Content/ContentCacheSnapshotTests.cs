using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Modules.Content.Caching;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentCacheSnapshotTests
{
    [Test]
    public async Task Content_item_snapshot_does_not_share_mutable_fields()
    {
        using var value = JsonDocument.Parse("""{"nested":{"title":"original"}}""");
        var source = new ContentItem
        {
            Id = 101,
            SiteId = 7,
            ContentTypeAlias = "feature",
            Culture = "en-US",
            Slug = "sample",
            Fields = new Dictionary<string, JsonElement>
            {
                ["body"] = value.RootElement.Clone()
            }
        };

        var firstRead = ContentCacheSnapshot.Clone(ContentCacheSnapshot.Clone(source));
        firstRead.Slug = "mutated";
        firstRead.Fields["body"] = JsonDocument.Parse("""{"nested":{"title":"changed"}}""")
            .RootElement.Clone();

        var secondRead = ContentCacheSnapshot.Clone(source);

        await Assert.That(secondRead.Slug).IsEqualTo("sample");
        await Assert.That(secondRead.Fields["body"].GetProperty("nested").GetProperty("title").GetString())
            .IsEqualTo("original");
    }

    [Test]
    public async Task Content_type_snapshot_does_not_share_fields_or_settings()
    {
        using var value = JsonDocument.Parse("""{"choices":["one","two"]}""");
        var source = new ContentTypeDefinition
        {
            Id = 202,
            SiteId = 7,
            Alias = "feature",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "heading",
                    Settings = new Dictionary<string, JsonElement>
                    {
                        ["editor"] = value.RootElement.Clone()
                    }
                }
            ]
        };

        var firstRead = ContentCacheSnapshot.Clone(ContentCacheSnapshot.Clone(source));
        firstRead.Fields[0].Name = "mutated";
        firstRead.Fields[0].Settings.Clear();

        var secondRead = ContentCacheSnapshot.Clone(source);

        await Assert.That(secondRead.Fields[0].Name).IsEqualTo("heading");
        await Assert.That(secondRead.Fields[0].Settings.ContainsKey("editor")).IsTrue();
    }
}
