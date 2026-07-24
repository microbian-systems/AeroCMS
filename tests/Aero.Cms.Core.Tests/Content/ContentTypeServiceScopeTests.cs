using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Templating;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentTypeServiceScopeTests
{
    [Test]
    public async Task Nonzero_missing_or_foreign_id_fails_while_same_alias_is_allowed_across_sites()
    {
        await using var harness = new SableTestHarness().WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument { Id = 1, SiteId = 2, Alias = "article", Name = "Foreign" });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(harness.Session, [], new ScribanTemplateValidator());

        var missing = await service.SaveAsync(new ContentTypeDefinition { Id = 99, SiteId = 1, Alias = "article", Name = "Missing" });
        var foreign = await service.SaveAsync(new ContentTypeDefinition { Id = 1, SiteId = 1, Alias = "article", Name = "Attacker" });
        var created = await service.SaveAsync(new ContentTypeDefinition
        {
            Id = 0, SiteId = 1, Alias = "article", Name = "Local",
            Fields = [new ContentFieldDefinition { Name = "title", FieldType = "text" }],
            ScribanTemplate = "<h1>{{ fields.title }}</h1>"
        });

        await Assert.That(missing.IsFailure).IsTrue();
        await Assert.That(foreign.IsFailure).IsTrue();
        await Assert.That(created.IsSuccess).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That((await verify.LoadAsync<ContentTypeDocument>(1))!.SiteId).IsEqualTo(2);
    }

    [Test]
    public async Task Existing_content_type_alias_requires_an_explicit_conversion_workflow()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 10,
            SiteId = 1,
            Alias = "article",
            Name = "Article"
        });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator());

        var result = await service.SaveAsync(new ContentTypeDefinition
        {
            Id = 10,
            SiteId = 1,
            Alias = "renamed-article",
            Name = "Article"
        });

        await Assert.That(result.IsFailure).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That((await verify.LoadAsync<ContentTypeDocument>(10))!.Alias)
            .IsEqualTo("article");
    }
}
