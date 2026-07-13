using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using System.Text.Json;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PageContentServiceHtmlTests
{
    [Test]
    public async Task SaveAsync_replaces_only_the_draft_and_preserves_the_published_snapshot()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSlugDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var page = CreatePage(9_301, CreateContent("Old draft"));
        page.PublishedContent = CreateContent("Published");
        page.PublicationState = ContentPublicationState.Published;
        page.ContentRevision = 4;
        page.PublishedVersion = 2;
        page.PublishedOn = DateTimeOffset.UtcNow.AddDays(-1);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        await using var editSession = await harness.OpenSessionAsync();
        var service = CreateService(editSession, page.SiteId);
        var editedPage = CreatePage(page.Id, CreateContent("New draft"));
        editedPage.PublicationState = ContentPublicationState.Draft;
        editedPage.PublishedContent = CreateContent("Attempted overwrite");

        var result = await service.SaveAsync(editedPage);

        result.IsSuccess.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.DraftContent.Root.Children[0].Children[0].Children[0].Text.ShouldBe("New draft");
        restored.PublishedContent!.Root.Children[0].Children[0].Children[0].Text.ShouldBe("Published");
        restored.PublicationState.ShouldBe(ContentPublicationState.Published);
        restored.ContentRevision.ShouldBe(5);
        restored.PublishedVersion.ShouldBe(2);
        restored.PublishedOn.ShouldNotBeNull();
        restored.PublishedOn!.Value.ToUnixTimeSeconds()
            .ShouldBe(page.PublishedOn!.Value.ToUnixTimeSeconds());
    }

    [Test]
    public async Task SaveAsync_rejects_invalid_html_before_mutating_the_stored_draft()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSlugDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var page = CreatePage(9_302, CreateContent("Stored draft"));
        page.ContentRevision = 3;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        await using var editSession = await harness.OpenSessionAsync();
        var service = CreateService(editSession, page.SiteId);
        var invalidEdit = CreatePage(page.Id, new HtmlPageContent
        {
            Root = HtmlNode.CreateElement("section")
        });

        var result = await service.SaveAsync(invalidEdit);

        result.IsFailure.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.DraftContent.Root.Children[0].Children[0].Children[0].Text.ShouldBe("Stored draft");
        restored.ContentRevision.ShouldBe(3);
    }

    [Test]
    public async Task UpdateAsync_rehydrates_source_generated_draft_content_transport()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSlugDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var page = CreatePage(9_303, CreateContent("Old transport draft"));
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        var replacement = CreateContent("Typed HTTP content");
        var transportJson = JsonSerializer.Serialize(
            replacement,
            HtmlJsonContext.Default.HtmlPageContent);

        await using var editSession = await harness.OpenSessionAsync();
        var service = CreateService(editSession, page.SiteId);
        var request = new Aero.Cms.Abstractions.Requests.UpdatePageRequest(
            page.Id,
            page.Title,
            page.Slug,
            page.Summary,
            page.SeoTitle,
            page.SeoDescription,
            page.PublicationState,
            DraftContentJson: transportJson);

        var result = await service.UpdateAsync(page.Id, request);

        result.IsSuccess.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.DraftContent.Root.Children[0].Children[0].Children[0].Text
            .ShouldBe("Typed HTTP content");
        restored.ContentRevision.ShouldBe(1);
    }

    private static AeroPageContentService CreateService(IDocumentSession session, long siteId)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(siteId);

        return new AeroPageContentService(
            session,
            Substitute.For<IMessageBus>(),
            siteContext,
            NullLogger<AeroPageContentService>.Instance,
            new HtmlContentValidator(
                catalog,
                new HtmlContentModelPolicy(catalog),
                new HtmlAttributePolicy()),
            new NativeCssStyleCompiler(),
            new NativeStyleProfile());
    }

    private static PageDocument CreatePage(long id, HtmlPageContent content) => new()
    {
        Id = id,
        SiteId = 42,
        Title = "HTML draft save test",
        Slug = $"html-draft-save-{id}",
        Path = $"/html-draft-save-{id}",
        DraftContent = content,
        PublicationState = ContentPublicationState.Draft
    };

    private static HtmlPageContent CreateContent(string text)
    {
        var section = HtmlNode.CreateElement("section");
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText(text));
        section.Children.Add(paragraph);
        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        return content;
    }
}
