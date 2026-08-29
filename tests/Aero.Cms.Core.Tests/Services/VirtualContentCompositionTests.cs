using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Html;
using Aero.Cms.Modules.Content.Composition;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public sealed class VirtualContentCompositionTests
{
    private static readonly ContentViewScope Scope = new(71, 42);

    [Test]
    public async Task Resolver_projects_a_provider_qualified_virtual_list_with_server_scope_and_bounded_paging()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.SearchAsync(Scope, "en-US", "sample", 5, Arg.Any<CancellationToken>()).Returns(new ContentEntry[]
        {
            new(new("view:catalog", "one"), Scope, new Dictionary<string, object?> { ["title"] = "Sample entry" }),
            new(new("view:catalog", "two"), Scope, new Dictionary<string, object?> { ["title"] = "Featured entry" }),
            new(new("view:catalog", "foreign"), new(Scope.TenantId, Scope.SiteId + 1), new Dictionary<string, object?> { ["title"] = "Hidden" })
        });
        var resolver = new ContentCompositionResolver(Substitute.For<IContentTypeService>(), Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(), [provider], new FixedSiteContext(Scope));

        var result = await resolver.ResolveListAsync(Scope.SiteId, "en-US", new PageContentListScope
        {
            NodeId = 9, ContentEntryProvider = "view:catalog", ContentTypeAlias = "catalog",
            Query = new PageContentListQuery { PageSize = 2, Filters = [new PageContentFilter { FieldName = "$search", Operator = PageContentFilterOperator.Contains, Value = "sample" }] }
        }, 2);

        var page = result.ShouldBeOfType<Result<PublishedContentPage, AeroError>.Ok>().Value;
        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(2);
        page.IsTotalCountExact.ShouldBeTrue();
        page.HasMore.ShouldBeFalse();
        await provider.Received(1).SearchAsync(Scope, "en-US", "sample", 5, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Resolver_rejects_virtual_search_field_aliases_instead_of_ignoring_them()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        var resolver = new ContentCompositionResolver(Substitute.For<IContentTypeService>(), Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(), [provider], new FixedSiteContext(Scope));

        var result = await resolver.ResolveListAsync(Scope.SiteId, "en-US", new PageContentListScope
        {
            NodeId = 9, ContentEntryProvider = "view:catalog", ContentTypeAlias = "catalog",
            Query = new PageContentListQuery { PageSize = 2, Filters = [new PageContentFilter { FieldName = "title", Operator = PageContentFilterOperator.Contains, Value = "sample" }] }
        }, 1);

        result.IsFailure.ShouldBeTrue();
        await provider.DidNotReceive().SearchAsync(Arg.Any<ContentViewScope>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Resolver_rejects_huge_page_number_before_overflow_or_provider_execution()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        var resolver = new ContentCompositionResolver(Substitute.For<IContentTypeService>(), Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(), [provider], new FixedSiteContext(Scope));

        var result = await resolver.ResolveListAsync(Scope.SiteId, "en-US", new PageContentListScope
        {
            NodeId = 9, ContentEntryProvider = "view:catalog", ContentTypeAlias = "catalog",
            Query = new PageContentListQuery { PageSize = PageContentListQuery.MaximumPageSize }
        }, int.MaxValue);

        result.IsFailure.ShouldBeTrue();
        await provider.DidNotReceive().SearchAsync(Arg.Any<ContentViewScope>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Resolver_reports_a_monotonic_lower_bound_for_a_nonfinal_page()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.SearchAsync(Scope, "en-US", null, 7, Arg.Any<CancellationToken>()).Returns(Enumerable.Range(1, 7)
            .Select(number => new ContentEntry(new("view:catalog", number.ToString()), Scope, new Dictionary<string, object?> { ["title"] = number }))
            .ToArray());
        var resolver = new ContentCompositionResolver(Substitute.For<IContentTypeService>(), Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(), [provider], new FixedSiteContext(Scope));

        var result = await resolver.ResolveListAsync(Scope.SiteId, "en-US", new PageContentListScope
        {
            NodeId = 9, ContentEntryProvider = "view:catalog", ContentTypeAlias = "catalog",
            Query = new PageContentListQuery { PageSize = 3 }
        }, 2);

        var page = result.ShouldBeOfType<Result<PublishedContentPage, AeroError>.Ok>().Value;
        page.Items.Count.ShouldBe(3);
        page.HasMore.ShouldBeTrue();
        page.IsTotalCountExact.ShouldBeFalse();
        page.TotalCount.ShouldBe(7);
    }

    [Test]
    public async Task Resolver_allows_the_exact_bounded_one_hundred_entry_window()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.SearchAsync(Scope, "en-US", null, 100, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 100)
                .Select(number => new ContentEntry(
                    new("view:catalog", number.ToString()),
                    Scope,
                    new Dictionary<string, object?> { ["title"] = number }))
                .ToArray());
        var resolver = new ContentCompositionResolver(
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(),
            [provider],
            new FixedSiteContext(Scope));

        var result = await resolver.ResolveListAsync(Scope.SiteId, "en-US", new PageContentListScope
        {
            NodeId = 9,
            ContentEntryProvider = "view:catalog",
            ContentTypeAlias = "catalog",
            Query = new PageContentListQuery { PageSize = 100 }
        }, 1);

        var page = result.ShouldBeOfType<Result<PublishedContentPage, AeroError>.Ok>().Value;
        page.Items.Count.ShouldBe(100);
        page.TotalCount.ShouldBe(100);
        page.HasMore.ShouldBeFalse();
        page.IsTotalCountExact.ShouldBeTrue();
        await provider.Received(1).SearchAsync(
            Scope, "en-US", null, 100, Arg.Any<CancellationToken>());
    }

    [Test]
    public void Composition_json_round_trips_virtual_and_existing_numeric_references()
    {
        var document = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope { NodeId = 10, ContentTypeId = 501, ContentTypeAlias = "articles", ContentItemId = 9001 },
                new PageContentItemScope { NodeId = 11, ContentEntryKey = new ContentEntryKey("registry", "entry-42") }
            ]
        };

        var json = JsonSerializer.Serialize(document, PageCompositionJsonContext.Default.PageCompositionDocument);
        var roundTrip = JsonSerializer.Deserialize(json, PageCompositionJsonContext.Default.PageCompositionDocument)!;

        roundTrip.ContentItems[0].ContentItemId.ShouldBe(9001);
        roundTrip.ContentItems[0].ContentEntryKey.ShouldBeNull();
        roundTrip.ContentItems[1].ContentEntryKey.ShouldBe(new ContentEntryKey("registry", "entry-42"));
    }

    [Test]
    public async Task Resolver_projects_site_scoped_virtual_entry_values_for_page_bindings()
    {
        var provider = Provider(new ContentEntry(
            new ContentEntryKey("registry", "entry-42"), Scope,
            new Dictionary<string, object?> { ["title"] = "Sample entry", ["category"] = "article" }));
        var resolver = new ContentCompositionResolver(
            Substitute.For<IContentTypeService>(), Substitute.For<IContentService>(), Substitute.For<IContentQueryService>(),
            [provider], new FixedSiteContext(Scope));

        var result = await resolver.ResolveItemAsync(42, "en-US", new PageContentItemScope
        {
            NodeId = 10,
            ContentEntryKey = new ContentEntryKey("registry", "entry-42")
        });

        var projection = result.ShouldBeOfType<Result<PublishedContentItemProjection, AeroError>.Ok>().Value;
        projection.Fields["title"].GetString().ShouldBe("Sample entry");
        projection.Fields["category"].GetString().ShouldBe("article");
    }

    [Test]
    public async Task Resolver_resolves_dynamic_view_provider_from_catalog_with_server_scope()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>())
            .Returns(new ContentEntry(
                new ContentEntryKey("view:catalog", "entry-42"),
                Scope,
                new Dictionary<string, object?> { ["title"] = "Sample entry" }));
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        catalog.ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>())
            .Returns(provider);
        var resolver = new ContentCompositionResolver(
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(),
            [],
            new FixedSiteContext(Scope),
            catalog);

        var result = await resolver.ResolveItemAsync(42, "en-US", new PageContentItemScope
        {
            NodeId = 10,
            ContentEntryKey = new ContentEntryKey("view:catalog", "entry-42")
        });

        result.ShouldBeOfType<Result<PublishedContentItemProjection, AeroError>.Ok>()
            .Value.Fields["title"].GetString().ShouldBe("Sample entry");
        await catalog.Received(1).ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>());
        await provider.Received(1).FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Validator_rejects_missing_provider_entry_and_cross_site_entry()
    {
        var contentTypes = Substitute.For<IContentTypeService>();
        var contentItems = Substitute.For<IContentService>();
        var missing = new ContentCompositionReferenceValidator(contentTypes, contentItems, [], new FixedSiteContext(Scope));
        var composition = VirtualComposition();
        (await missing.ValidateAsync(42, "en-US", composition, ContentReferenceValidationMode.Publishing)).IsFailure.ShouldBeTrue();

        var foreign = Provider(new ContentEntry(
            new ContentEntryKey("registry", "entry-42"), new ContentViewScope(71, 43),
            new Dictionary<string, object?>()));
        var validator = new ContentCompositionReferenceValidator(contentTypes, contentItems, [foreign], new FixedSiteContext(Scope));
        (await validator.ValidateAsync(42, "en-US", composition, ContentReferenceValidationMode.Publishing)).IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task Publishing_validator_blocks_missing_dynamic_view_entry()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>())
            .Returns((ContentEntry?)null);
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        catalog.ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>())
            .Returns(provider);
        var validator = new ContentCompositionReferenceValidator(
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentService>(),
            [],
            new FixedSiteContext(Scope),
            catalog);
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = 10,
                    ContentEntryKey = new ContentEntryKey("view:catalog", "entry-42")
                }
            ]
        };

        var result = await validator.ValidateAsync(
            Scope.SiteId,
            "en-US",
            composition,
            ContentReferenceValidationMode.Publishing);

        result.IsFailure.ShouldBeTrue();
        await catalog.Received(1).ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>());
        await provider.Received(1).FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Expander_applies_virtual_projection_through_existing_field_binding()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var section = catalog.CreateElement("section");
        var title = catalog.CreateElement("h1");
        title.Children.Add(HtmlNode.CreateText("placeholder"));
        section.Children.Add(title);
        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = section.NodeId,
                    ContentEntryKey = new ContentEntryKey("view:catalog", "entry-42")
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = title.NodeId,
                    ScopeNodeId = section.NodeId,
                    FieldName = "title"
                }
            ]
        };
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>())
            .Returns(new ContentEntry(
                new ContentEntryKey("view:catalog", "entry-42"),
                Scope,
                new Dictionary<string, object?> { ["title"] = "Sample entry" }));
        var providerCatalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        providerCatalog.ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>())
            .Returns(provider);
        var resolver = new ContentCompositionResolver(
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(),
            [],
            new FixedSiteContext(Scope),
            providerCatalog);

        var expander = new PageCompositionExpander(resolver,
            new HtmlContentValidator(catalog, new HtmlContentModelPolicy(catalog), new HtmlAttributePolicy()));
        var result = await expander.ExpandAsync(42, "en-US", content, composition);

        result.IsSuccess.ShouldBeTrue();
        var expanded = ((Result<PageCompositionExpansion, AeroError>.Ok)result).Value.Content;
        HtmlTreeOperations.FindById(expanded.Root, title.NodeId)!.Children.Single().Text.ShouldBe("Sample entry");
    }

    [Test]
    public async Task Route_bound_expander_materializes_only_the_persisted_parameter_as_the_stable_id()
    {
        var (content, composition, titleId) = RouteBoundPage();
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>())
            .Returns(new ContentEntry(
                new ContentEntryKey("view:catalog", "entry-42"),
                Scope,
                new Dictionary<string, object?> { ["title"] = "Sample entry" }));
        var providerCatalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        providerCatalog.ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>())
            .Returns(provider);
        var resolver = new ContentCompositionResolver(
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(),
            [],
            new FixedSiteContext(Scope),
            providerCatalog);
        var htmlCatalog = HtmlElementCatalog.CreateDefault();
        var expander = new PageCompositionExpander(
            resolver,
            new HtmlContentValidator(htmlCatalog, new HtmlContentModelPolicy(htmlCatalog), new HtmlAttributePolicy()));

        var result = await expander.ExpandAsync(
            Scope.SiteId,
            "en-US",
            content,
            composition,
            routeValues: new Dictionary<string, string>
            {
                ["ignored"] = "must-not-be-used",
                ["entryId"] = "entry-42"
            });

        var expanded = result.ShouldBeOfType<Result<PageCompositionExpansion, AeroError>.Ok>().Value.Content;
        HtmlTreeOperations.FindById(expanded.Root, titleId)!.Children.Single().Text.ShouldBe("Sample entry");
        await provider.Received(1).FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>());
        await provider.DidNotReceive().FindAsync(Scope, "must-not-be-used", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Route_bound_expander_returns_not_found_when_the_parameter_or_entry_is_missing()
    {
        var (content, composition, _) = RouteBoundPage();
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.FindAsync(Scope, "missing", Arg.Any<CancellationToken>()).Returns((ContentEntry?)null);
        var providerCatalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        providerCatalog.ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>()).Returns(provider);
        var resolver = new ContentCompositionResolver(
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(),
            [],
            new FixedSiteContext(Scope),
            providerCatalog);
        var htmlCatalog = HtmlElementCatalog.CreateDefault();
        var expander = new PageCompositionExpander(
            resolver,
            new HtmlContentValidator(htmlCatalog, new HtmlContentModelPolicy(htmlCatalog), new HtmlAttributePolicy()));

        var absentParameter = await expander.ExpandAsync(
            Scope.SiteId, "en-US", content, composition, routeValues: new Dictionary<string, string>());
        absentParameter.ShouldBeOfType<Result<PageCompositionExpansion, AeroError>.Failure>()
            .Error.ShouldBeOfType<AeroError.NotFound>();

        var missingEntry = await expander.ExpandAsync(
            Scope.SiteId,
            "en-US",
            content,
            composition,
            routeValues: new Dictionary<string, string> { ["entryId"] = "missing" });
        missingEntry.ShouldBeOfType<Result<PageCompositionExpansion, AeroError>.Failure>()
            .Error.ShouldBeOfType<AeroError.NotFound>();
    }

    [Test]
    public async Task Publishing_validator_checks_route_bound_provider_but_not_an_unknown_concrete_id()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        var providerCatalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        providerCatalog.ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>()).Returns(provider);
        var validator = new ContentCompositionReferenceValidator(
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentService>(),
            [],
            new FixedSiteContext(Scope),
            providerCatalog);
        var composition = RouteBoundPage().Composition;

        var result = await validator.ValidateAsync(
            Scope.SiteId,
            "en-US",
            composition,
            ContentReferenceValidationMode.Publishing);

        result.IsSuccess.ShouldBeTrue();
        await providerCatalog.Received(1).ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>());
        await provider.DidNotReceive().FindAsync(
            Arg.Any<ContentViewScope>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static (HtmlPageContent Content, PageCompositionDocument Composition, long TitleId) RouteBoundPage()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var section = catalog.CreateElement("section");
        var title = catalog.CreateElement("h1");
        title.Children.Add(HtmlNode.CreateText("placeholder"));
        section.Children.Add(title);
        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = section.NodeId,
                    ContentEntryKey = new ContentEntryKey("view:catalog", string.Empty),
                    StableIdRouteParameter = "entryId"
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = title.NodeId,
                    ScopeNodeId = section.NodeId,
                    FieldName = "title"
                }
            ]
        };
        return (content, composition, title.NodeId);
    }

    private static PageCompositionDocument VirtualComposition(long scopeNodeId = 10, long bindingNodeId = 11) => new()
    {
        ContentItems = [new PageContentItemScope { NodeId = scopeNodeId, ContentEntryKey = new ContentEntryKey("registry", "entry-42") }],
        FieldBindings = [new PageFieldBinding { NodeId = bindingNodeId, ScopeNodeId = scopeNodeId, FieldName = "title" }]
    };

    private static IContentEntrySourceProvider Provider(ContentEntry? entry)
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("registry");
        provider.FindAsync(Arg.Any<ContentViewScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(entry);
        return provider;
    }

    private sealed class FixedSiteContext(ContentViewScope scope) : ISiteContext
    {
        public long SiteId => scope.SiteId;
        public long TenantId => scope.TenantId;
    }
}
