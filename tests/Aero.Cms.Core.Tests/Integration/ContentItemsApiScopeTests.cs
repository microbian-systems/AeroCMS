using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
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
        IContentQueryService query)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton(actor);
        builder.Services.AddSingleton(query);
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
