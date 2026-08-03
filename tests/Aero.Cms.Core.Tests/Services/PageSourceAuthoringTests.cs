using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PageSourceAuthoringTests
{
    private SableTestHarness _harness = null!;
    private IPageRenderer _renderer = null!;
    private IPageContentQueryResolver _queryResolver = null!;
    private AeroPageContentService _service = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<PageSourceVersion>(SchemaMode.Flexible)
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithSchema<ContentSlugDocument>();
        await _harness.InitializeAsync();

        _renderer = Substitute.For<IPageRenderer>();
        _renderer.Descriptor.Returns(new PageRendererDescriptor(
            PageRendererIds.Scriban,
            "Scriban",
            PageEditorKinds.Source,
            SupportsFragments: true,
            IsExperimental: false,
            SourceLanguage: "liquid"));
        _renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<RenderedPage>>(
                new Result<RenderedPage>.Ok(new RenderedPage("<main>ok</main>", string.Empty, []))));

        var registry = Substitute.For<IPageRendererRegistry>();
        registry.Resolve(PageRendererIds.Scriban)
            .Returns(new Result<IPageRenderer>.Ok(_renderer));

        _queryResolver = Substitute.For<IPageContentQueryResolver>();
        _queryResolver.ResolveAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ContentQueryDefinition>?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<PageContentQueryResolution>>(
                new Result<PageContentQueryResolution>.Ok(PageContentQueryResolution.Empty)));

        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(42);
        _service = new AeroPageContentService(
            _harness.Session,
            Substitute.For<IMessageBus>(),
            siteContext,
            NullLogger<AeroPageContentService>.Instance,
            CreateContentValidator(),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            actor: "editor@example.test",
            pageRendererRegistry: registry,
            pageSourceVersionStore: new PageSourceVersionStore(_harness.Session),
            pageContentQueryResolver: _queryResolver);
    }

    [After(Test)]
    public async Task TearDown()
        => await _harness.DisposeAsync();

    [Test]
    public async Task Create_stores_exact_source_and_page_pointer_in_one_commit()
    {
        const string exactSource = "\r\n  <main>{{ page.title }}</main>\n";

        var result = await _service.CreateAsync(CreateRequest(exactSource));

        var created = Success(result);
        created.DraftSourceVersionId.ShouldNotBeNull();
        created.PublishedSourceVersionId.ShouldBeNull();

        await using var verificationSession = await _harness.OpenSessionAsync();
        var storedPage = await verificationSession.LoadAsync<PageDocument>(created.Id);
        storedPage.ShouldNotBeNull();
        var storedSource = await verificationSession.LoadAsync<PageSourceVersion>(
            created.DraftSourceVersionId!.Value);
        storedSource.ShouldNotBeNull();
        storedSource.PageId.ShouldBe(created.Id);
        storedSource.SiteId.ShouldBe(42);
        storedSource.RendererId.ShouldBe(PageRendererIds.Scriban);
        storedSource.Source.ShouldBe(exactSource);

        await _queryResolver.Received(1).ResolveAsync(
            42,
            "en-US",
            Arg.Any<IReadOnlyList<ContentQueryDefinition>?>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Update_with_identical_exact_source_preserves_the_version()
    {
        const string exactSource = "<main>{{ page.title }}</main>";
        var created = Success(await _service.CreateAsync(CreateRequest(exactSource)));
        var originalVersion = created.DraftSourceVersionId;

        var result = await _service.UpdateAsync(
            created.Id,
            UpdateRequest(created, exactSource, title: "Updated title"));

        var updated = Success(result);
        updated.DraftSourceVersionId.ShouldBe(originalVersion);
        (await _harness.Session.Query<PageSourceVersion>().ToListAsync()).Count.ShouldBe(1);
        await _renderer.Received(1).RenderAsync(
            Arg.Any<PageRenderRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Update_with_changed_source_stages_a_new_owned_version()
    {
        const string originalSource = "<main>one</main>";
        const string replacementSource = "\r\n<main>two</main>\n";
        var created = Success(await _service.CreateAsync(CreateRequest(originalSource)));
        var originalVersion = created.DraftSourceVersionId;

        var result = await _service.UpdateAsync(
            created.Id,
            UpdateRequest(created, replacementSource));

        var updated = Success(result);
        updated.DraftSourceVersionId.ShouldNotBe(originalVersion);
        var versions = await _harness.Session.Query<PageSourceVersion>().ToListAsync();
        versions.Count.ShouldBe(2);
        versions.Single(version => version.Id == updated.DraftSourceVersionId).Source
            .ShouldBe(replacementSource);
    }

    [Test]
    public async Task Renderer_validation_failure_commits_neither_page_nor_source()
    {
        _renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<RenderedPage>>(
                new Result<RenderedPage>.Failure(
                    AeroError.ValidationError(["Source preview failed."]))));

        var result = await _service.CreateAsync(CreateRequest("<main>broken</main>"));

        result.IsFailure.ShouldBeTrue();
        await using var verificationSession = await _harness.OpenSessionAsync();
        (await verificationSession.Query<PageDocument>().ToListAsync()).ShouldBeEmpty();
        (await verificationSession.Query<PageSourceVersion>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Culture_fork_clones_source_into_a_version_owned_by_the_target_page()
    {
        _harness.Session.Store(new SitesModel
        {
            Id = 42,
            DefaultCulture = "en-US",
            SupportedCultures = ["en-US", "es-MX"]
        });
        await _harness.Session.SaveChangesAsync();
        const string exactSource = "\r\n<main>{{ page.title }}</main>\n";
        var sourcePage = Success(await _service.CreateAsync(CreateRequest(exactSource)));

        var result = await _service.ForkPageForCultureAsync(
            sourcePage.Id,
            "es-MX",
            "pagina-fuente");

        var fork = Success(result);
        fork.DraftSourceVersionId.ShouldNotBeNull();
        fork.DraftSourceVersionId.ShouldNotBe(sourcePage.DraftSourceVersionId);
        fork.PublishedSourceVersionId.ShouldBeNull();
        var forkSource = await _harness.Session.LoadAsync<PageSourceVersion>(
            fork.DraftSourceVersionId.Value);
        forkSource.ShouldNotBeNull();
        forkSource.PageId.ShouldBe(fork.Id);
        forkSource.SiteId.ShouldBe(fork.SiteId);
        forkSource.RendererId.ShouldBe(PageRendererIds.Scriban);
        forkSource.Source.ShouldBe(exactSource);
    }

    [Test]
    public async Task Source_load_fails_closed_when_the_version_belongs_to_another_page()
    {
        var sourceStore = new PageSourceVersionStore(_harness.Session);
        var foreignSource = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                42,
                999,
                PageRendererIds.Scriban,
                "<main>foreign</main>",
                DateTimeOffset.UtcNow));
        var page = new PageDocument
        {
            Id = 800,
            SiteId = 42,
            Title = "Owned page",
            Slug = "owned-page",
            Path = "/owned-page",
            RendererId = PageRendererIds.Scriban,
            DraftSourceVersionId = foreignSource.Value.Id
        };
        _harness.Session.Store(page);
        await _harness.Session.SaveChangesAsync();

        var result = await _service.LoadDraftSourceAsync(page.Id);

        result.IsFailure.ShouldBeTrue();
        ((Result<PageSourceVersionSnapshot, AeroError>.Failure)result).Error
            .ShouldBeOfType<AeroError.NotFound>();
    }

    private static CreatePageRequest CreateRequest(string source)
        => new(
            "Source page",
            "source-page",
            null,
            null,
            null,
            RendererId: PageRendererIds.Scriban,
            DraftSource: source);

    private static PageDocument Success(Result<PageDocument, AeroError> result)
        => result is Result<PageDocument, AeroError>.Ok success
            ? success.Value
            : throw new InvalidOperationException(
                ((Result<PageDocument, AeroError>.Failure)result).Error.ToString());

    private static UpdatePageRequest UpdateRequest(
        PageDocument page,
        string source,
        string? title = null)
        => new(
            page.Id,
            title ?? page.Title,
            page.Slug,
            page.Summary,
            page.SeoTitle,
            page.SeoDescription,
            ParentId: page.ParentId,
            ShowInNavMenu: page.ShowInNavMenu,
            ShowHeaderNavigation: page.ShowHeaderNavigation,
            HideFooter: page.HideFooter,
            ShowChatAgent: page.ShowChatAgent,
            RendererId: PageRendererIds.Scriban,
            DraftSource: source);

    private static IHtmlContentValidator CreateContentValidator()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        return new HtmlContentValidator(
            catalog,
            new HtmlContentModelPolicy(catalog),
            new HtmlAttributePolicy());
    }

    private static ISiteStyleProfileResolver CreateStyleProfileResolver()
    {
        var resolver = Substitute.For<ISiteStyleProfileResolver>();
        resolver.ResolveAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IStyleProfile, AeroError>>(
                new Result<IStyleProfile, AeroError>.Ok(new NativeStyleProfile())));
        return resolver;
    }
}
