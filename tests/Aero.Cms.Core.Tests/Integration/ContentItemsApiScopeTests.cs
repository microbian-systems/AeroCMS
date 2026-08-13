using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Areas.Api.v1;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class ContentItemsApiScopeTests
{
    private const long SiteId = 8311;
    private const long ItemId = 10;
    private const string ActorFailure = "Content type or related content was not found.";

    [Test]
    [Arguments("get")]
    [Arguments("update")]
    [Arguments("delete")]
    [Arguments("publish")]
    [Arguments("unpublish")]
    [Arguments("translations")]
    [Arguments("fork")]
    public async Task Same_site_wrong_id_actor_data_is_not_found_before_mutation(string operation)
    {
        var actor = Substitute.For<IAeroContentItemActor>();
        actor.GetByIdAsync(ItemId, SiteId, Arg.Any<CancellationToken>())
            .Returns(SuccessfulItem(ItemId + 1));
        var query = Substitute.For<IContentQueryService>();
        await using var app = await CreateAppAsync(actor, query);

        using var response = await app.GetTestClient().SendAsync(CreateRequest(operation));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await actor.DidNotReceiveWithAnyArgs().SaveDraftAsync(default!, default, default);
        await actor.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default);
        await actor.DidNotReceiveWithAnyArgs().PublishAsync(default, default, default);
        await actor.DidNotReceiveWithAnyArgs().UnpublishAsync(default, default, default);
        await query.DidNotReceiveWithAnyArgs().ListCultureVariantsAsync(
            default,
            default!,
            default,
            default);
    }

    [Test]
    [Arguments("create")]
    [Arguments("update")]
    [Arguments("delete")]
    [Arguments("publish")]
    [Arguments("unpublish")]
    [Arguments("fork")]
    public async Task Actor_mutation_failure_after_valid_scope_returns_generic_bad_request(
        string operation)
    {
        var actor = Substitute.For<IAeroContentItemActor>();
        actor.GetByIdAsync(ItemId, SiteId, Arg.Any<CancellationToken>())
            .Returns(SuccessfulItem(ItemId));
        actor.SaveDraftAsync(
                Arg.Any<ContentItemViewModel>(),
                SiteId,
                Arg.Any<CancellationToken>())
            .Returns(FailedMutation());
        actor.DeleteAsync(ItemId, SiteId, Arg.Any<CancellationToken>())
            .Returns(FailedMutation());
        actor.PublishAsync(ItemId, SiteId, Arg.Any<CancellationToken>())
            .Returns(FailedMutation());
        actor.UnpublishAsync(ItemId, SiteId, Arg.Any<CancellationToken>())
            .Returns(FailedMutation());
        var query = Substitute.For<IContentQueryService>();
        query.ListCultureVariantsAsync(
                SiteId,
                "article",
                ItemId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IReadOnlyList<ContentItem>, AeroError>>(
                new Result<IReadOnlyList<ContentItem>, AeroError>.Ok([])));
        await using var app = await CreateAppAsync(actor, query);

        using var response = await app.GetTestClient().SendAsync(CreateRequest(operation));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body.ToLowerInvariant()).DoesNotContain(ActorFailure.ToLowerInvariant());
    }

    [Test]
    public async Task Missing_authoritative_localization_context_rejects_fork_before_handler()
    {
        var actor = Substitute.For<IAeroContentItemActor>();
        actor.GetByIdAsync(ItemId, SiteId, Arg.Any<CancellationToken>()).Returns(SuccessfulItem(ItemId));
        var query = Substitute.For<IContentQueryService>();
        var localization = Substitute.For<IContentLocalizationHandler>();
        var contextResolver = Substitute.For<IContentLocalizationContextResolver>();
        contextResolver.ResolveAsync(SiteId, "article", Arg.Any<CancellationToken>()).Returns((ContentLocalizationContext?)null);
        await using var app = await CreateAppAsync(actor, query, localization, contextResolver);

        using var response = await app.GetTestClient().SendAsync(CreateRequest("fork"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await localization.DidNotReceiveWithAnyArgs().ForkAsync(default!, default!, default);
    }

    [Test]
    public async Task Get_returns_the_actor_item_and_translation_group_storage_tokens()
    {
        var actor = Substitute.For<IAeroContentItemActor>();
        var stored = SuccessfulItem(ItemId);
        stored.data.StorageVersion = 17;
        stored.data.TranslationGroupRevision = 5;
        stored.data.TranslationGroupStorageVersion = 23;
        actor.GetByIdAsync(ItemId, SiteId, Arg.Any<CancellationToken>()).Returns(stored);
        await using var app = await CreateAppAsync(actor, Substitute.For<IContentQueryService>());

        using var response = await app.GetTestClient().SendAsync(CreateRequest("get"));
        var detail = await response.Content.ReadFromJsonAsync<ContentItemDetail>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(detail).IsNotNull();
        await Assert.That(detail!.StorageVersion).IsEqualTo(17);
        await Assert.That(detail.TranslationGroupRevision).IsEqualTo(5);
        await Assert.That(detail.TranslationGroupStorageVersion).IsEqualTo(23);
    }

    [Test]
    public async Task Update_preserves_the_persisted_translation_culture()
    {
        var actor = Substitute.For<IAeroContentItemActor>();
        var stored = SuccessfulItem(ItemId);
        stored.data.Culture = "es-MX";
        actor.GetByIdAsync(ItemId, SiteId, Arg.Any<CancellationToken>()).Returns(stored);
        actor.SaveDraftAsync(
                Arg.Any<ContentItemViewModel>(),
                SiteId,
                Arg.Any<CancellationToken>())
            .Returns(call => new AeroRequestResponse<ContentItemViewModel>(
                call.Arg<ContentItemViewModel>(),
                new ContentItemErrorViewModel()));
        await using var app = await CreateAppAsync(actor, Substitute.For<IContentQueryService>());

        using var response = await app.GetTestClient().SendAsync(CreateRequest("update"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await actor.Received(1).SaveDraftAsync(
            Arg.Is<ContentItemViewModel>(item => item.Culture == "es-MX"),
            SiteId,
            Arg.Any<CancellationToken>());
    }

    private static AeroRequestResponse<ContentItemViewModel> SuccessfulItem(long id) =>
        new(
            new ContentItemViewModel
            {
                Id = id,
                SiteId = SiteId,
                ContentTypeAlias = "article",
                Title = "Title",
                Slug = "entry",
                Culture = "en-US",
                TranslationGroupId = ItemId,
                FieldsJson = "{}"
            },
            new ContentItemErrorViewModel());

    private static AeroRequestResponse<ContentItemViewModel> FailedMutation() =>
        new(
            new ContentItemViewModel(),
            new ContentItemErrorViewModel { Message = ActorFailure });

    private static HttpRequestMessage CreateRequest(string operation)
    {
        var itemBody = new CreateContentItemRequest(
            "Title",
            "entry",
            new Dictionary<string, System.Text.Json.JsonElement>(),
            null,
            null,
            "en-US");
        var request = operation switch
        {
            "create" => new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/admin/content-items/article")
            {
                Content = JsonContent.Create(itemBody)
            },
            "update" => new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/v1/admin/content-items/article/{ItemId}")
            {
                Content = JsonContent.Create(itemBody)
            },
            "delete" => new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/v1/admin/content-items/article/{ItemId}"),
            "publish" => new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/v1/admin/content-items/article/{ItemId}/publish"),
            "unpublish" => new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/v1/admin/content-items/article/{ItemId}/unpublish"),
            "translations" => new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/admin/content-items/article/{ItemId}/translations"),
            "fork" => new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/v1/admin/content-items/article/{ItemId}/translations")
            {
                Content = JsonContent.Create(new ForkContentItemCultureRequest("fr-FR", "entree"))
            },
            _ => new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/admin/content-items/article/{ItemId}")
        };
        return request.WithTestUser(8312);
    }

    private static async Task<WebApplication> CreateAppAsync(
        IAeroContentItemActor actor,
        IContentQueryService query,
        IContentLocalizationHandler? localization = null,
        IContentLocalizationContextResolver? contextResolver = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton(actor);
        builder.Services.AddSingleton(query);
        localization ??= Substitute.For<IContentLocalizationHandler>();
        localization.ForkAsync(
                Arg.Any<ContentLocalizationContext>(),
                Arg.Any<ContentCultureForkCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentLocalizationOperationResult, AeroError>>(
                AeroError.InvalidRequestError("rejected")));
        builder.Services.AddSingleton(localization);
        if (contextResolver is null)
        {
            contextResolver = Substitute.For<IContentLocalizationContextResolver>();
            contextResolver.ResolveAsync(SiteId, "article", Arg.Any<CancellationToken>())
                .Returns(new ContentLocalizationContext(SiteId, "en-US", ["en-US", "fr-FR"], ContentCultureFallbackPolicy.ExactOnly));
        }
        builder.Services.AddSingleton(contextResolver);
        var site = Substitute.For<ISiteContext>();
        site.SiteId.Returns(SiteId);
        builder.Services.AddSingleton(site);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapContentItemsApi();
        await app.StartAsync();
        return app;
    }
}
