using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.OutputCache.Caching;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Shouldly;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PageRouteTemplateTests
{
    [Test]
    [Arguments("/catalog/{entryId}", "entryId")]
    [Arguments("/articles/profile/{article_id}", "article_id")]
    public void Parse_accepts_one_safe_named_segment(string input, string parameterName)
    {
        var template = PageRouteTemplate.Parse(input)
            .ShouldBeOfType<Result<PageRouteTemplate, AeroError>.Ok>().Value;

        template.ParameterName.ShouldBe(parameterName);
    }

    [Test]
    [Arguments("catalog/{id}")]
    [Arguments("/catalog/{id}/")]
    [Arguments("/catalog/{*id}")]
    [Arguments("/catalog/{id?}")]
    [Arguments("/catalog/{id:int}")]
    [Arguments("/catalog/{id}/{other}")]
    [Arguments("/Catalog/{id}")]
    public void Parse_rejects_routes_outside_the_narrow_grammar(string input)
    {
        PageRouteTemplate.Parse(input).IsFailure.ShouldBeTrue();
    }

    [Test]
    [Arguments("entry-42")]
    [Arguments("urn:catalog:entry-42")]
    [Arguments("source_id-1.2~draft")]
    public void Match_accepts_bounded_unreserved_source_identifiers(string stableId)
    {
        var template = Parse("/catalog/{entryId}");

        template.TryMatch($"/catalog/{stableId}", out var matched).ShouldBeTrue();
        matched.ShouldBe(stableId);
    }

    [Test]
    [Arguments("<script>")]
    [Arguments("bad?id")]
    [Arguments("bad#id")]
    [Arguments("bad%2Fid")]
    [Arguments("quoted\"id")]
    [Arguments("..")]
    public void Match_rejects_values_that_are_unsafe_to_reuse_in_canonical_urls(string stableId)
    {
        Parse("/catalog/{entryId}").TryMatch($"/catalog/{stableId}", out _).ShouldBeFalse();
    }

    [Test]
    public void Overlap_is_detected_even_when_parameter_positions_differ()
    {
        Parse("/catalog/{id}/profile").Overlaps(Parse("/catalog/featured/{id}"))
            .ShouldBeTrue();
        Parse("/catalog/{id}").Overlaps(Parse("/articles/{id}"))
            .ShouldBeFalse();
    }

    [Test]
    public void Composition_binding_requires_the_declared_route_parameter()
    {
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = 10,
                    ContentEntryKey = new ContentEntryKey("view:catalog", string.Empty),
                    StableIdRouteParameter = "articleId"
                }
            ]
        };

        PageRouteTemplate.ValidateCompositionBindings("/catalog/{entryId}", composition)
            .IsFailure.ShouldBeTrue();
        PageRouteTemplate.ValidateCompositionBindings("/articles/{articleId}", composition)
            .IsSuccess.ShouldBeTrue();
    }

    [Test]
    public void Published_match_is_site_and_culture_scoped_and_returns_the_actual_path()
    {
        var candidates = new[]
        {
            Page(1, 42, "fr-FR", "/catalog/{entryId}"),
            Page(2, 42, "en-US", "/catalog/{entryId}"),
            Page(3, 99, "en-US", "/catalog/{entryId}")
        };

        var match = PageRouteTemplateService.SelectPublishedMatch(
                42,
                "en-US",
                "/catalog/entry-42",
                candidates)
            .ShouldBeOfType<Result<PageRouteTemplateMatch?, AeroError>.Ok>().Value;

        match.ShouldNotBeNull();
        match.PageId.ShouldBe(2);
        match.Culture.ShouldBe("en-US");
        match.ResolvedPath.ShouldBe("/catalog/entry-42");
        match.RouteValues.ShouldHaveSingleItem().Value.ShouldBe("entry-42");
    }

    [Test]
    public void Published_match_fails_closed_when_the_bounded_candidate_cap_is_exceeded()
    {
        var candidates = Enumerable.Range(1, PageRouteTemplateService.MaximumTemplateCandidates + 1)
            .Select(id => Page(id, 42, "en-US", $"/catalog-{id}/{{entryId}}"))
            .ToArray();

        PageRouteTemplateService.SelectPublishedMatch(42, "en-US", "/catalog-1/entry-42", candidates)
            .IsFailure.ShouldBeTrue();
    }

    [Test]
    public void Published_match_rejects_ambiguous_legacy_templates_instead_of_guessing()
    {
        var candidates = new[]
        {
            Page(1, 42, "en-US", "/catalog/{entryId}"),
            Page(2, 42, "en-US", "/catalog/{recordId}")
        };

        PageRouteTemplateService.SelectPublishedMatch(42, "en-US", "/catalog/entry-42", candidates)
            .IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task Output_cache_key_partitions_each_resolved_detail_path()
    {
        var first = CacheContext("/catalog/entry-42");
        var second = CacheContext("/catalog/entry-99");

        await ((IOutputCachePolicy)CmsOutputCachePolicy.Instance)
            .CacheRequestAsync(first, CancellationToken.None);
        await ((IOutputCachePolicy)CmsOutputCachePolicy.Instance)
            .CacheRequestAsync(second, CancellationToken.None);

        first.CacheVaryByRules.VaryByValues["path"].ShouldBe("/catalog/entry-42");
        second.CacheVaryByRules.VaryByValues["path"].ShouldBe("/catalog/entry-99");
        first.CacheVaryByRules.VaryByValues["path"]
            .ShouldNotBe(second.CacheVaryByRules.VaryByValues["path"]);
    }

    [Test]
    public async Task Output_cache_tags_virtual_provider_pages_for_view_invalidation()
    {
        var context = CacheContext("/catalog/entry-42");
        context.HttpContext.Items["AeroCms.SiteId"] = 42L;
        context.HttpContext.Items["AeroCms.TenantId"] = 71L;
        context.HttpContext.Items["AeroCms.ContentViewProviders"] = new[] { "view:catalog" };

        await ((IOutputCachePolicy)CmsOutputCachePolicy.Instance)
            .ServeResponseAsync(context, CancellationToken.None);

        var scope = new ContentViewScope(71, 42);
        context.Tags.ShouldContain(ContentViewOutputCacheTags.Site(scope));
        context.Tags.ShouldContain(ContentViewOutputCacheTags.Provider(scope, "view:catalog"));
    }

    [Test]
    public void Publishing_copies_route_template_into_the_immutable_public_snapshot()
    {
        var page = Page(42, 7, "en-US", "/old/{id}");
        page.DraftRouteTemplate = "/catalog/{entryId}";

        page.PublishDraftContent(DateTimeOffset.UtcNow);

        page.PublishedRouteTemplate.ShouldBe("/catalog/{entryId}");
        page.ToViewModel().PublishedRouteTemplate.ShouldBe("/catalog/{entryId}");
    }

    private static PageRouteTemplate Parse(string input)
        => PageRouteTemplate.Parse(input)
            .ShouldBeOfType<Result<PageRouteTemplate, AeroError>.Ok>().Value;

    private static PageDocument Page(long id, long siteId, string culture, string template) => new()
    {
        Id = id,
        SiteId = siteId,
        Culture = culture,
        Slug = $"template-{id}",
        Title = $"Template {id}",
        PublicationState = ContentPublicationState.Published,
        PublishedRouteTemplate = template
    };

    private static OutputCacheContext CacheContext(string path)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Get;
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("example.test");
        http.Request.Path = path;
        return new OutputCacheContext { HttpContext = http };
    }
}
