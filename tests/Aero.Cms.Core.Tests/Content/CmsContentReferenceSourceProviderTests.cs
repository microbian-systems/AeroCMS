using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Core.Railway;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class CmsContentReferenceSourceProviderTests
{
    [Test]
    public async Task Page_search_filters_by_site_and_culture_before_applying_the_result_limit()
    {
        const long siteId = 42;
        const long matchingPageId = 900;
        const long otherSitePageId = 901;

        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        for (var index = 0; index < 105; index++)
        {
            harness.Session.Store(Page(
                id: index + 1,
                siteId,
                "en-US",
                $"Page {index:D3}",
                $"page-{index:D3}"));
        }

        harness.Session.Store(
            Page(
                matchingPageId,
                siteId,
                "en-US",
                "Zebra needle page",
                "zebra-needle"),
            Page(
                otherSitePageId,
                siteId + 1,
                "en-US",
                "Foreign needle page",
                "foreign-needle"),
            Page(
                902,
                siteId,
                "fr-FR",
                "French needle page",
                "french-needle"));
        await harness.Session.SaveChangesAsync();

        var provider = new PageContentReferenceSourceProvider(harness.Session);

        var result = await provider.SearchAsync(
            siteId,
            "en-US",
            "needle",
            take: 10);
        var options = result
            .ShouldBeOfType<Result<IReadOnlyList<CmsContentReferenceOption>>.Ok>()
            .Value;

        options.Count.ShouldBe(1);
        options[0].Id.ShouldBe(matchingPageId.ToString());
        (await provider.ExistsAsync(siteId, matchingPageId))
            .ShouldBeOfType<Result<bool>.Ok>()
            .Value.ShouldBeTrue();
        (await provider.ExistsAsync(siteId, otherSitePageId))
            .ShouldBeOfType<Result<bool>.Ok>()
            .Value.ShouldBeFalse();
    }

    private static PageDocument Page(
        long id,
        long siteId,
        string culture,
        string title,
        string slug)
    {
        return new PageDocument
        {
            Id = id,
            SiteId = siteId,
            Culture = culture,
            Title = title,
            Slug = slug,
            Path = $"/{slug}"
        };
    }
}
