using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Services;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentServiceScopeTests
{
    [Test]
    public async Task Id_operations_are_site_scoped_and_foreign_update_cannot_rehome()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument { Id = 10, SiteId = 1, Alias = "article", Name = "Article" });
        harness.Session.Store(new ContentItem { Id = 20, SiteId = 2, ContentTypeAlias = "article", Culture = "en-US", Slug = "foreign" });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentService(harness.Session);

        await Assert.That((await service.LoadAsync(1, 20)).IsFailure).IsTrue();
        await Assert.That(await service.ExistsAsync(1, 20)).IsFalse();
        await Assert.That((await service.DeleteAsync(1, 20)).IsFailure).IsTrue();
        var rehome = await service.SaveAsync(new ContentItem
        {
            Id = 20, SiteId = 1, ContentTypeAlias = "article", Culture = "en-US", Slug = "attacker"
        });
        await Assert.That(rehome.IsFailure).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That((await verify.LoadAsync<ContentItem>(20))!.SiteId).IsEqualTo(2);
    }

    [Test]
    public async Task Create_requires_same_site_type_and_all_references_atomically()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        using var multiple = JsonDocument.Parse("true");
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 30, SiteId = 1, Alias = "article", Name = "Article",
            Fields = [new ContentFieldDefinition { Name = "related", FieldType = "reference", Settings = new() { ["allowMultiple"] = multiple.RootElement.Clone() } }]
        });
        harness.Session.Store(
            new ContentItem { Id = 31, SiteId = 1, ContentTypeAlias = "article", Culture = "en-US", Slug = "local" },
            new ContentItem { Id = 32, SiteId = 2, ContentTypeAlias = "article", Culture = "en-US", Slug = "foreign" });
        await harness.Session.SaveChangesAsync();
        using var refs = JsonDocument.Parse("""["31","32"]""");
        var service = new AeroContentService(harness.Session);
        var attempted = new ContentItem
        {
            Id = 0, SiteId = 1, ContentTypeAlias = "article", Culture = "en-US", Slug = "new",
            Fields = new() { ["related"] = refs.RootElement.Clone() }
        };
        var result = await service.SaveAsync(attempted);
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(attempted.Id).IsEqualTo(0);
        await Assert.That((await harness.Session.Query<ContentItem>().Where(x => x.Slug == "new").ToListAsync())).IsEmpty();
    }

    [Test]
    public async Task Create_accepts_an_empty_optional_single_reference()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 35,
            SiteId = 1,
            Alias = "article",
            Name = "Article",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "related",
                    FieldType = "reference",
                    Required = false
                }
            ]
        });
        await harness.Session.SaveChangesAsync();
        using var emptyReference = JsonDocument.Parse("\"\"");
        var service = new AeroContentService(harness.Session);

        var item = new ContentItem
        {
            Id = 0,
            SiteId = 1,
            ContentTypeAlias = "article",
            Culture = "en-US",
            Slug = "new",
            Fields = new() { ["related"] = emptyReference.RootElement.Clone() }
        };
        var result = await service.SaveAsync(item);

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        await Assert.That((await harness.Session.Query<ContentItem>().Where(x => x.Slug == "new").ToListAsync())).HasSingleItem();
    }

    [Test]
    public async Task Create_persists_hyphenated_dynamic_field_names()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 36,
            SiteId = 1,
            Alias = "article",
            Name = "Article",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "title-2",
                    FieldType = "short-text"
                }
            ]
        });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentService(harness.Session);

        var item = new ContentItem
        {
            Id = 0,
            SiteId = 1,
            ContentTypeAlias = "article",
            Culture = "en-US",
            Slug = "hyphenated-field",
            Fields = new()
            {
                ["title-2"] = JsonSerializer.SerializeToElement("test")
            }
        };
        var result = await service.SaveAsync(item);

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        await using var verify = await harness.Store.QuerySessionAsync();
        var stored = await verify.LoadAsync<ContentItem>(item.Id);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.Fields["title-2"].GetString()).IsEqualTo("test");
    }

    [Test]
    public async Task Create_rejects_foreign_source_and_translation_group()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument { Id = 40, SiteId = 1, Alias = "article", Name = "Article" });
        harness.Session.Store(new ContentItem { Id = 41, SiteId = 2, ContentTypeAlias = "article", Culture = "en-US", Slug = "foreign", TranslationGroupId = 41 });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentService(harness.Session);
        var source = await service.SaveAsync(new ContentItem { SiteId = 1, ContentTypeAlias = "article", Culture = "en-US", Slug = "source", SourceItemId = 41 });
        var group = await service.SaveAsync(new ContentItem { SiteId = 1, ContentTypeAlias = "article", Culture = "en-US", Slug = "group", TranslationGroupId = 41 });
        await Assert.That(source.IsFailure).IsTrue();
        await Assert.That(group.IsFailure).IsTrue();
    }
}
