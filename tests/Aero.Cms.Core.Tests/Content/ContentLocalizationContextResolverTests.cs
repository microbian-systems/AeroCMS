using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentLocalizationContextResolverTests
{
    [Test]
    public async Task Missing_or_disabled_site_denies_before_content_type_lookup()
    {
        var session = Substitute.For<IQuerySession>();
        var types = Substitute.For<IContentTypeService>();
        session.LoadAsync<SitesModel>(7, Arg.Any<CancellationToken>()).Returns((SitesModel?)null);
        var resolver = new ContentLocalizationContextResolver(session, types);

        (await resolver.ResolveAsync(7, "article")).ShouldBeNull();
        await types.DidNotReceiveWithAnyArgs().GetByAliasAsync(default, default!, default);

        session.LoadAsync<SitesModel>(7, Arg.Any<CancellationToken>()).Returns(new SitesModel { Id = 7, IsEnabled = false });
        (await resolver.ResolveAsync(7, "article")).ShouldBeNull();
        await types.DidNotReceiveWithAnyArgs().GetByAliasAsync(default, default!, default);
    }

    [Test]
    public async Task Valid_enabled_site_returns_persisted_cultures_and_type_fallback_policy()
    {
        var session = Substitute.For<IQuerySession>();
        var types = Substitute.For<IContentTypeService>();
        session.LoadAsync<SitesModel>(7, Arg.Any<CancellationToken>()).Returns(new SitesModel
        {
            Id = 7, IsEnabled = true, DefaultCulture = "en-us", SupportedCultures = ["en-us", "fr-fr"]
        });
        var type = new ContentTypeDefinition { Id = 9, SiteId = 7, Alias = "article" };
        type.Localization.CultureFallbackPolicy = ContentCultureFallbackPolicy.ParentCultureThenDefaultCulture;
        types.GetByAliasAsync(7, "article", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(type));

        var context = await new ContentLocalizationContextResolver(session, types).ResolveAsync(7, "article");

        context.ShouldNotBeNull();
        context.DefaultCulture.ShouldBe("en-US");
        context.SupportedCultures.ShouldBe(["en-US", "fr-FR"]);
        context.CultureFallbackPolicy.ShouldBe(ContentCultureFallbackPolicy.ParentCultureThenDefaultCulture);
    }
}
