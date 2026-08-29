using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Railway;
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
        page.DraftComposition = CreateItemComposition(page.DraftContent, 7001);
        page.PublishedContent = CreateContent("Published");
        page.PublishedComposition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = page.PublishedContent.Root.Children[0].NodeId,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    ContentItemId = 6001
                }
            ]
        };
        page.PublicationState = ContentPublicationState.Published;
        page.ContentRevision = 4;
        page.PublishedVersion = 2;
        page.PublishedOn = DateTimeOffset.UtcNow.AddDays(-1);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        await using var editSession = await harness.OpenSessionAsync();
        var service = CreateService(editSession, page.SiteId);
        var editedPage = CreatePage(page.Id, CreateContent("New draft"));
        editedPage.DraftComposition = CreateItemComposition(editedPage.DraftContent, 7002);
        editedPage.PublicationState = ContentPublicationState.Draft;
        editedPage.PublishedContent = CreateContent("Attempted overwrite");

        var result = await service.SaveAsync(editedPage);

        result.IsSuccess.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.DraftContent.Root.Children[0].Children[0].Children[0].Text.ShouldBe("New draft");
        restored.DraftComposition.ContentItems[0].ContentItemId.ShouldBe(7002);
        restored.PublishedContent!.Root.Children[0].Children[0].Children[0].Text.ShouldBe("Published");
        restored.PublishedComposition!.ContentItems[0].ContentItemId.ShouldBe(6001);
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
    public async Task SaveAsync_rejects_invalid_content_references_before_mutating_the_stored_draft()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSlugDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var page = CreatePage(9_304, CreateContent("Stored draft"));
        page.ContentRevision = 3;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        var referenceValidator = Substitute.For<IContentCompositionReferenceValidator>();
        referenceValidator.ValidateAsync(
                Arg.Any<Aero.Cms.Abstractions.Content.Views.ContentViewScope>(),
                Arg.Any<string>(),
                Arg.Any<PageCompositionDocument>(),
                ContentReferenceValidationMode.Authoring,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<bool, AeroError>>(
                new Result<bool, AeroError>.Failure(
                    AeroError.ValidationError(["The selected content item no longer exists."]))));
        await using var editSession = await harness.OpenSessionAsync();
        var service = CreateService(editSession, page.SiteId, referenceValidator);
        var invalidEdit = CreatePage(page.Id, CreateContent("Rejected draft"));
        invalidEdit.DraftComposition = CreateItemComposition(invalidEdit.DraftContent, 7_002);

        var result = await service.SaveAsync(invalidEdit);

        result.IsFailure.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.DraftContent.Root.Children[0].Children[0].Children[0].Text
            .ShouldBe("Stored draft");
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
        var replacementComposition = CreateItemComposition(replacement, 8_001);
        var compositionJson = JsonSerializer.Serialize(
            replacementComposition,
            PageCompositionJsonContext.Default.PageCompositionDocument);

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
            DraftContentJson: transportJson,
            DraftCompositionJson: compositionJson);

        var result = await service.UpdateAsync(page.Id, request);

        result.IsSuccess.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.DraftContent.Root.Children[0].Children[0].Children[0].Text
            .ShouldBe("Typed HTTP content");
        restored.DraftComposition.ContentItems[0].ContentItemId.ShouldBe(8_001);
        restored.ContentRevision.ShouldBe(1);
    }

    [Test]
    public async Task UpdateAsync_rejects_the_candidate_composition_before_replacing_the_tracked_draft()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSlugDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var page = CreatePage(9_305, CreateContent("Stored transport draft"));
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        var replacement = CreateContent("Rejected transport draft");
        var scope = replacement.Root.Children[0];
        var invalidComposition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = scope.NodeId,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    TemplateRootNodeId = scope.Children[0].NodeId,
                    Query = null!
                }
            ]
        };
        var contentJson = JsonSerializer.Serialize(
            replacement,
            HtmlJsonContext.Default.HtmlPageContent);
        var compositionJson = JsonSerializer.Serialize(
            invalidComposition,
            PageCompositionJsonContext.Default.PageCompositionDocument);
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
            DraftContentJson: contentJson,
            DraftCompositionJson: compositionJson);

        var result = await service.UpdateAsync(page.Id, request);

        result.IsFailure.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.DraftContent.Root.Children[0].Children[0].Children[0].Text
            .ShouldBe("Stored transport draft");
        restored.ContentRevision.ShouldBe(0);
    }

    [Test]
    public async Task CreateAsync_validates_the_submitted_composition_against_the_submitted_html()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSlugDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var content = CreateContent("New page draft");
        var invalidComposition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = 9_999_999,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    ContentItemId = 7_001
                }
            ]
        };
        var contentJson = JsonSerializer.Serialize(
            content,
            HtmlJsonContext.Default.HtmlPageContent);
        var compositionJson = JsonSerializer.Serialize(
            invalidComposition,
            PageCompositionJsonContext.Default.PageCompositionDocument);

        await using var editSession = await harness.OpenSessionAsync();
        var service = CreateService(editSession, 42);
        var request = new Aero.Cms.Abstractions.Requests.CreatePageRequest(
            "Invalid composition",
            "invalid-composition",
            null,
            null,
            null,
            DraftContentJson: contentJson,
            DraftCompositionJson: compositionJson);

        var result = await service.CreateAsync(request);

        result.IsFailure.ShouldBeTrue();
    }

    private static AeroPageContentService CreateService(
        IDocumentSession session,
        long siteId,
        IContentCompositionReferenceValidator? referenceValidator = null)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(siteId);
        if (referenceValidator is null)
        {
            referenceValidator = Substitute.For<IContentCompositionReferenceValidator>();
            referenceValidator.ValidateAsync(
                    Arg.Any<Aero.Cms.Abstractions.Content.Views.ContentViewScope>(),
                    Arg.Any<string>(),
                    Arg.Any<PageCompositionDocument>(),
                    Arg.Any<ContentReferenceValidationMode>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Result<bool, AeroError>>(
                    new Result<bool, AeroError>.Ok(true)));
        }

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
            CreateStyleProfileResolver(),
            contentReferenceValidator: referenceValidator);
    }

    private static ISiteStyleProfileResolver CreateStyleProfileResolver()
    {
        var resolver = Substitute.For<ISiteStyleProfileResolver>();
        resolver.ResolveAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IStyleProfile, AeroError>>(
                new Result<IStyleProfile, AeroError>.Ok(new NativeStyleProfile())));
        return resolver;
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

    private static PageCompositionDocument CreateItemComposition(
        HtmlPageContent content,
        long contentItemId)
    {
        var scope = content.Root.Children[0];
        var target = scope.Children[0];
        return new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = scope.NodeId,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    ContentItemId = contentItemId,
                    Slug = $"article-{contentItemId}"
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = target.NodeId,
                    ScopeNodeId = scope.NodeId,
                    FieldName = "title",
                    Target = PageFieldBindingTarget.TextContent
                }
            ]
        };
    }
}
