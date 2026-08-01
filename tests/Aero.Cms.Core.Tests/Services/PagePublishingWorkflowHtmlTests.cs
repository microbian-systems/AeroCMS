using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PagePublishingWorkflowHtmlTests
{
    [Test]
    public async Task PublishNowAsync_validates_and_persists_an_independent_published_snapshot()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var page = CreatePage(9_201, CreateValidContent("Publish me"));
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var service = CreateService(harness.Session);

        var result = await service.PublishNowAsync(page.Id);

        result.IsSuccess.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.PublicationState.ShouldBe(ContentPublicationState.Published);
        restored.PublishedVersion.ShouldBe(1);
        restored.PublishedContent.ShouldNotBeNull();
        restored.PublishedContent!.Root.Children[0].Children[0].Children[0].Text.ShouldBe("Publish me");
        restored.DraftContent.Root.Children[0].Children[0].Children[0].Text = "Later draft";
        restored.PublishedContent.Root.Children[0].Children[0].Children[0].Text.ShouldBe("Publish me");
    }

    [Test]
    public async Task PublishNowAsync_rejects_an_invalid_draft_without_mutating_publication_state()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var invalid = new HtmlPageContent
        {
            Root = HtmlNode.CreateElement("section")
        };
        var page = CreatePage(9_202, invalid);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var service = CreateService(harness.Session);

        var result = await service.PublishNowAsync(page.Id);

        result.IsFailure.ShouldBeTrue();
        page.PublicationState.ShouldBe(ContentPublicationState.Draft);
        page.PublishedVersion.ShouldBe(0);
        page.PublishedContent.ShouldBeNull();
    }

    [Test]
    public async Task PublishNowAsync_rejects_stale_content_references_before_snapshotting()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var page = CreatePage(9_205, CreateValidContent("Do not publish stale content"));
        var scope = page.DraftContent.Root.Children[0];
        var target = scope.Children[0];
        page.DraftComposition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = scope.NodeId,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    ContentItemId = 7_001
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = target.NodeId,
                    ScopeNodeId = scope.NodeId,
                    FieldName = "title"
                }
            ]
        };
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var referenceValidator = Substitute.For<IContentCompositionReferenceValidator>();
        referenceValidator.ValidateAsync(
                page.SiteId,
                Arg.Any<string>(),
                Arg.Any<PageCompositionDocument>(),
                ContentReferenceValidationMode.Publishing,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<bool, AeroError>>(
                new Result<bool, AeroError>.Failure(
                    AeroError.ValidationError(["A referenced content item is not published."]))));
        var service = CreateService(harness.Session, referenceValidator);

        var result = await service.PublishNowAsync(page.Id);

        result.IsFailure.ShouldBeTrue();
        page.PublicationState.ShouldBe(ContentPublicationState.Draft);
        page.PublishedVersion.ShouldBe(0);
        page.PublishedContent.ShouldBeNull();
        page.PublishedComposition.ShouldBeNull();
    }

    [Test]
    public async Task PublishNowAsync_WithAuthorizedSite_RejectsCrossSitePageWithoutMutation()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var page = CreatePage(9_204, CreateValidContent("Do not publish"));
        page.SiteId = 99;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var service = CreateService(harness.Session);

        var result = await service.PublishNowAsync(page.Id, authorizedSiteId: 7);

        result.IsFailure.ShouldBeTrue();
        ((Result<bool, AeroError>.Failure)result).Error.ShouldBeOfType<AeroError.NotFound>();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.PublicationState.ShouldBe(ContentPublicationState.Draft);
        restored.PublishedVersion.ShouldBe(0);
        restored.PublishedContent.ShouldBeNull();
    }

    [Test]
    public async Task SubmitForReviewAsync_validates_the_draft_and_saves_state_directly()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var page = CreatePage(9_203, CreateValidContent("Review me"));
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var service = CreateService(harness.Session);

        var result = await service.SubmitForReviewAsync(page.Id);

        result.IsSuccess.ShouldBeTrue();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.PublicationState.ShouldBe(ContentPublicationState.InReview);
        restored.PublishedContent.ShouldBeNull();
    }

    [Test]
    public async Task PublishNowAsync_Scriban_preloads_exact_source_and_renders_before_snapshotting()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<PageSourceVersion>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        const string exactSource = "\r\n<main>{{ page.title }}</main>\n";
        var page = CreatePage(9_206, CreateValidContent("Source draft"));
        page.RendererId = PageRendererIds.Scriban;
        var sourceStore = new PageSourceVersionStore(harness.Session);
        var source = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                page.SiteId,
                page.Id,
                PageRendererIds.Scriban,
                exactSource,
                DateTimeOffset.UtcNow));
        page.DraftSourceVersionId = source.Value.Id;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        PageRenderRequest? captured = null;
        var renderer = Substitute.For<IPageRenderer>();
        renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<PageRenderRequest>();
                return Task.FromResult<Result<RenderedPage>>(
                    new Result<RenderedPage>.Ok(
                        new RenderedPage("<main>rendered</main>", string.Empty, [])));
            });
        var queryResolver = CreateQueryResolver();
        var service = CreateSourceService(
            harness.Session,
            sourceStore,
            renderer,
            queryResolver);

        var result = await service.PublishNowAsync(page.Id);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured!.IsPreview.ShouldBeFalse();
        captured.Source.ShouldNotBeNull();
        captured.Source!.VersionId.ShouldBe(source.Value.Id);
        captured.Source.Source.ShouldBe(exactSource);
        await using var verificationSession = await harness.OpenSessionAsync();
        var published = await verificationSession.LoadAsync<PageDocument>(page.Id);
        published.ShouldNotBeNull();
        published!.PublishedSourceVersionId.ShouldBe(source.Value.Id);
        published.PublicationState.ShouldBe(ContentPublicationState.Published);
        published.PublishedVersion.ShouldBe(1);
        await queryResolver.Received(1).ResolveAsync(
            page.SiteId,
            page.Culture,
            Arg.Any<IReadOnlyList<ContentQueryDefinition>?>(),
            false,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishNowAsync_Scriban_render_failure_preserves_all_published_state()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<PageSourceVersion>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var page = CreatePage(9_207, CreateValidContent("New draft"));
        page.RendererId = PageRendererIds.Scriban;
        page.PublishedContent = CreateValidContent("Prior published");
        page.PublishedComposition = new PageCompositionDocument();
        page.PublishedVersion = 4;
        var sourceStore = new PageSourceVersionStore(harness.Session);
        var priorSource = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                page.SiteId,
                page.Id,
                PageRendererIds.Scriban,
                "<main>prior</main>",
                DateTimeOffset.UtcNow));
        var draftSource = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                page.SiteId,
                page.Id,
                PageRendererIds.Scriban,
                "<main>broken</main>",
                DateTimeOffset.UtcNow));
        page.PublishedSourceVersionId = priorSource.Value.Id;
        page.DraftSourceVersionId = draftSource.Value.Id;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var renderer = Substitute.For<IPageRenderer>();
        renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<RenderedPage>>(
                new Result<RenderedPage>.Failure(
                    AeroError.ValidationError(["Source render failed."]))));
        var service = CreateSourceService(
            harness.Session,
            sourceStore,
            renderer,
            CreateQueryResolver());

        var result = await service.PublishNowAsync(page.Id);

        result.IsFailure.ShouldBeTrue();
        page.PublicationState.ShouldBe(ContentPublicationState.Draft);
        page.PublishedVersion.ShouldBe(4);
        page.PublishedSourceVersionId.ShouldBe(priorSource.Value.Id);
        ReadHeading(page.PublishedContent!).ShouldBe("Prior published");
        page.PublishedComposition.ShouldNotBeNull();
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);
        restored.ShouldNotBeNull();
        restored!.PublishedVersion.ShouldBe(4);
        restored.PublishedSourceVersionId.ShouldBe(priorSource.Value.Id);
        ReadHeading(restored.PublishedContent!).ShouldBe("Prior published");
    }

    [Test]
    public async Task PublishBatchAsync_TwoScribanVariants_PublishesBothSourceSnapshots()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<PageSourceVersion>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var english = CreatePage(9_208, CreateValidContent("English draft"));
        english.Culture = "en-US";
        var french = CreatePage(9_209, CreateValidContent("French draft"));
        french.Culture = "fr-FR";
        english.RendererId = PageRendererIds.Scriban;
        french.RendererId = PageRendererIds.Scriban;
        var sourceStore = new PageSourceVersionStore(harness.Session);
        const string englishSource = "\r\n<main>English {{ page.title }}</main>\n";
        const string frenchSource = "\n<main>Français {{ page.title }}</main>\r\n";
        var englishVersion = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                english.SiteId,
                english.Id,
                PageRendererIds.Scriban,
                englishSource,
                DateTimeOffset.UtcNow));
        var frenchVersion = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                french.SiteId,
                french.Id,
                PageRendererIds.Scriban,
                frenchSource,
                DateTimeOffset.UtcNow));
        english.DraftSourceVersionId = englishVersion.Value.Id;
        french.DraftSourceVersionId = frenchVersion.Value.Id;
        harness.Session.Store(english, french);
        await harness.Session.SaveChangesAsync();
        var renderedRequests = new List<PageRenderRequest>();
        var renderer = Substitute.For<IPageRenderer>();
        renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                renderedRequests.Add(call.Arg<PageRenderRequest>());
                return Task.FromResult<Result<RenderedPage>>(
                    new Result<RenderedPage>.Ok(
                        new RenderedPage("<main>rendered</main>", string.Empty, [])));
            });
        var service = CreateSourceService(
            harness.Session,
            sourceStore,
            renderer,
            CreateQueryResolver());

        var result = await service.PublishBatchAsync(
            [english.Id, french.Id],
            authorizedSiteId: english.SiteId);

        result.IsSuccess.ShouldBeTrue();
        renderedRequests.Select(request => request.Metadata.Id)
            .ShouldBe([english.Id, french.Id]);
        renderedRequests.Single(request => request.Metadata.Id == english.Id)
            .Source!.Source.ShouldBe(englishSource);
        renderedRequests.Single(request => request.Metadata.Id == french.Id)
            .Source!.Source.ShouldBe(frenchSource);
        await using var verificationSession = await harness.OpenSessionAsync();
        var published = await verificationSession.LoadManyAsync<PageDocument>(
            [english.Id, french.Id]);
        var publishedEnglish = published.Single(page => page.Id == english.Id);
        var publishedFrench = published.Single(page => page.Id == french.Id);
        publishedEnglish.PublicationState.ShouldBe(ContentPublicationState.Published);
        publishedFrench.PublicationState.ShouldBe(ContentPublicationState.Published);
        publishedEnglish.PublishedSourceVersionId.ShouldBe(englishVersion.Value.Id);
        publishedFrench.PublishedSourceVersionId.ShouldBe(frenchVersion.Value.Id);
        publishedEnglish.PublishedVersion.ShouldBe(1);
        publishedFrench.PublishedVersion.ShouldBe(1);
    }

    [Test]
    public async Task PublishBatchAsync_WhenOneScribanRendererFails_PreservesEveryVariant()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<PageSourceVersion>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var first = CreatePage(9_210, CreateValidContent("First draft"));
        var failing = CreatePage(9_211, CreateValidContent("Failing draft"));
        first.Culture = "en-US";
        failing.Culture = "fr-FR";
        first.RendererId = PageRendererIds.Scriban;
        failing.RendererId = PageRendererIds.Scriban;
        first.PublishedContent = CreateValidContent("First prior");
        failing.PublishedContent = CreateValidContent("Failing prior");
        first.PublishedComposition = new PageCompositionDocument();
        failing.PublishedComposition = new PageCompositionDocument();
        first.PublishedVersion = 2;
        failing.PublishedVersion = 3;
        var sourceStore = new PageSourceVersionStore(harness.Session);
        var firstPrior = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                first.SiteId,
                first.Id,
                PageRendererIds.Scriban,
                "<main>first prior</main>",
                DateTimeOffset.UtcNow));
        var firstDraft = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                first.SiteId,
                first.Id,
                PageRendererIds.Scriban,
                "<main>first draft</main>",
                DateTimeOffset.UtcNow));
        var failingPrior = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                failing.SiteId,
                failing.Id,
                PageRendererIds.Scriban,
                "<main>failing prior</main>",
                DateTimeOffset.UtcNow));
        var failingDraft = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                failing.SiteId,
                failing.Id,
                PageRendererIds.Scriban,
                "<main>failing draft</main>",
                DateTimeOffset.UtcNow));
        first.PublishedSourceVersionId = firstPrior.Value.Id;
        first.DraftSourceVersionId = firstDraft.Value.Id;
        failing.PublishedSourceVersionId = failingPrior.Value.Id;
        failing.DraftSourceVersionId = failingDraft.Value.Id;
        harness.Session.Store(first, failing);
        await harness.Session.SaveChangesAsync();
        var renderedPageIds = new List<long>();
        var renderer = Substitute.For<IPageRenderer>();
        renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<PageRenderRequest>();
                renderedPageIds.Add(request.Metadata.Id!.Value);
                return request.Metadata.Id == failing.Id
                    ? Task.FromResult<Result<RenderedPage>>(
                        new Result<RenderedPage>.Failure(
                            AeroError.ValidationError(["Source render failed."])))
                    : Task.FromResult<Result<RenderedPage>>(
                        new Result<RenderedPage>.Ok(
                            new RenderedPage("<main>rendered</main>", string.Empty, [])));
            });
        var service = CreateSourceService(
            harness.Session,
            sourceStore,
            renderer,
            CreateQueryResolver());

        var result = await service.PublishBatchAsync(
            [first.Id, failing.Id],
            authorizedSiteId: first.SiteId);

        result.IsFailure.ShouldBeTrue();
        renderedPageIds.ShouldBe([first.Id, failing.Id]);
        first.PublicationState.ShouldBe(ContentPublicationState.Draft);
        failing.PublicationState.ShouldBe(ContentPublicationState.Draft);
        first.PublishedVersion.ShouldBe(2);
        failing.PublishedVersion.ShouldBe(3);
        first.PublishedSourceVersionId.ShouldBe(firstPrior.Value.Id);
        failing.PublishedSourceVersionId.ShouldBe(failingPrior.Value.Id);
        first.DraftSourceVersionId.ShouldBe(firstDraft.Value.Id);
        failing.DraftSourceVersionId.ShouldBe(failingDraft.Value.Id);
        ReadHeading(first.PublishedContent!).ShouldBe("First prior");
        ReadHeading(failing.PublishedContent!).ShouldBe("Failing prior");
        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadManyAsync<PageDocument>(
            [first.Id, failing.Id]);
        var restoredFirst = restored.Single(page => page.Id == first.Id);
        var restoredFailing = restored.Single(page => page.Id == failing.Id);
        restoredFirst.PublicationState.ShouldBe(ContentPublicationState.Draft);
        restoredFailing.PublicationState.ShouldBe(ContentPublicationState.Draft);
        restoredFirst.PublishedVersion.ShouldBe(2);
        restoredFailing.PublishedVersion.ShouldBe(3);
        restoredFirst.PublishedSourceVersionId.ShouldBe(firstPrior.Value.Id);
        restoredFailing.PublishedSourceVersionId.ShouldBe(failingPrior.Value.Id);
        restoredFirst.DraftSourceVersionId.ShouldBe(firstDraft.Value.Id);
        restoredFailing.DraftSourceVersionId.ShouldBe(failingDraft.Value.Id);
        ReadHeading(restoredFirst.PublishedContent!).ShouldBe("First prior");
        ReadHeading(restoredFailing.PublishedContent!).ShouldBe("Failing prior");
    }

    private static PagePublishingWorkflowService CreateService(
        IDocumentSession session,
        IContentCompositionReferenceValidator? referenceValidator = null)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var contentPolicy = new HtmlContentModelPolicy(catalog);
        var attributePolicy = new HtmlAttributePolicy();
        return new PagePublishingWorkflowService(
            session,
            Substitute.For<IMessageBus>(),
            new HtmlContentValidator(catalog, contentPolicy, attributePolicy),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            NullLogger<PagePublishingWorkflowService>.Instance,
            referenceValidator);
    }

    private static PagePublishingWorkflowService CreateSourceService(
        IDocumentSession session,
        IPageSourceVersionStore sourceStore,
        IPageRenderer renderer,
        IPageContentQueryResolver queryResolver)
    {
        var registry = Substitute.For<IPageRendererRegistry>();
        registry.Resolve(PageRendererIds.Scriban)
            .Returns(new Result<IPageRenderer>.Ok(renderer));
        var catalog = HtmlElementCatalog.CreateDefault();
        return new PagePublishingWorkflowService(
            session,
            Substitute.For<IMessageBus>(),
            new HtmlContentValidator(
                catalog,
                new HtmlContentModelPolicy(catalog),
                new HtmlAttributePolicy()),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            NullLogger<PagePublishingWorkflowService>.Instance,
            pageRendererRegistry: registry,
            pageSourceVersionStore: sourceStore,
            pageContentQueryResolver: queryResolver);
    }

    private static IPageContentQueryResolver CreateQueryResolver()
    {
        var resolver = Substitute.For<IPageContentQueryResolver>();
        resolver.ResolveAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ContentQueryDefinition>?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<PageContentQueryResolution>>(
                new Result<PageContentQueryResolution>.Ok(PageContentQueryResolution.Empty)));
        return resolver;
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
        SiteId = 7,
        Title = "HTML publishing test",
        Slug = $"html-publishing-{id}",
        DraftContent = content,
        PublicationState = ContentPublicationState.Draft
    };

    private static HtmlPageContent CreateValidContent(string text)
    {
        var section = HtmlNode.CreateElement("section");
        var heading = HtmlNode.CreateElement("h2");
        heading.Children.Add(HtmlNode.CreateText(text));
        section.Children.Add(heading);
        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        return content;
    }

    private static string? ReadHeading(HtmlPageContent content)
        => content.Root.Children[0].Children[0].Children[0].Text;
}
