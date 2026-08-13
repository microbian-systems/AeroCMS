using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentAiTranslationSnapshotResolverTests
{
    [Test]
    public async Task Missing_source_fails_without_loading_type_or_context()
    {
        var content = Substitute.For<IContentService>();
        var types = Substitute.For<IContentTypeService>();
        var contexts = Substitute.For<IContentLocalizationContextResolver>();
        content.LoadAsync(7, 10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(AeroError.NotFoundError("missing")));
        content.LoadAsync(7, 20, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(new ContentItem { Id = 20, SiteId = 7 }));

        var result = await new ContentAiTranslationSnapshotResolver(content, types, contexts).ResolveAsync(7, 10, 20);

        result.ShouldBeOfType<Result<ContentAiTranslationGenerationSnapshot>.Failure>();
        await types.DidNotReceiveWithAnyArgs().GetByAliasAsync(default, default!, default);
        await contexts.DidNotReceiveWithAnyArgs().ResolveAsync(default, default!, default);
    }

    [Test]
    public async Task Wrong_group_or_type_fails_before_loading_authoritative_context()
    {
        var content = Substitute.For<IContentService>();
        var types = Substitute.For<IContentTypeService>();
        var contexts = Substitute.For<IContentLocalizationContextResolver>();
        content.LoadAsync(7, 10, Arg.Any<CancellationToken>()).Returns(Item(10, "article", 12, 4, "en-US"));
        content.LoadAsync(7, 20, Arg.Any<CancellationToken>()).Returns(Item(20, "other", 99, 6, "fr-FR"));

        var result = await new ContentAiTranslationSnapshotResolver(content, types, contexts).ResolveAsync(7, 10, 20);

        result.ShouldBeOfType<Result<ContentAiTranslationGenerationSnapshot>.Failure>();
        await types.DidNotReceiveWithAnyArgs().GetByAliasAsync(default, default!, default);
        await contexts.DidNotReceiveWithAnyArgs().ResolveAsync(default, default!, default);
    }

    [Test]
    public async Task Valid_variants_return_only_persisted_authoritative_snapshot()
    {
        var content = Substitute.For<IContentService>();
        var types = Substitute.For<IContentTypeService>();
        var contexts = Substitute.For<IContentLocalizationContextResolver>();
        content.LoadAsync(7, 10, Arg.Any<CancellationToken>()).Returns(Item(10, "article", 12, 4, "en-US"));
        content.LoadAsync(7, 20, Arg.Any<CancellationToken>()).Returns(Item(20, "article", 12, 6, "fr-FR"));
        types.GetByAliasAsync(7, "article", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(new ContentTypeDefinition { SiteId = 7, Alias = "article" }));
        contexts.ResolveAsync(7, "article", Arg.Any<CancellationToken>())
            .Returns(new ContentLocalizationContext(7, "en-US", ["en-US", "fr-FR"], ContentCultureFallbackPolicy.ExactOnly));

        var result = await new ContentAiTranslationSnapshotResolver(content, types, contexts).ResolveAsync(7, 10, 20);

        var snapshot = result.ShouldBeOfType<Result<ContentAiTranslationGenerationSnapshot>.Ok>().Value;
        snapshot.Source.ContentItemId.ShouldBe(10);
        snapshot.Source.VersionNumber.ShouldBe(4);
        snapshot.Target.ContentItemId.ShouldBe(20);
        snapshot.Target.VersionNumber.ShouldBe(6);
        snapshot.Localization.SiteId.ShouldBe(7);
    }

    private static Task<Result<ContentItem, AeroError>> Item(long id, string type, long group, int version, string culture) =>
        Task.FromResult<Result<ContentItem, AeroError>>(new ContentItem
        {
            Id = id, SiteId = 7, ContentTypeAlias = type, TranslationGroupId = group,
            VersionNumber = version, Culture = culture,
            Fields = new() { ["title"] = System.Text.Json.JsonSerializer.SerializeToElement("trusted") }
        });
}
