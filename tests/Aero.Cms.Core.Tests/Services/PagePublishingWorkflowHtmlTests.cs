using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages;
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

    private static PagePublishingWorkflowService CreateService(IDocumentSession session)
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
            NullLogger<PagePublishingWorkflowService>.Instance);
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
}
