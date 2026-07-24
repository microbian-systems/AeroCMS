using Aero.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Events;
using Aero.Cms.Modules.Content.Grains;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Wolverine;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentGrainScopeTests
{
    [Test]
    public async Task Content_item_inherited_identifier_and_request_crud_fail_closed_without_scope()
    {
        var scopes = Substitute.For<IServiceScopeFactory>();
        var grain = new AeroContentItemGrain(NullLogger<AeroActor>.Instance, scopes);
        var request = Substitute.For<IRequest>();

        var get = await grain.GetByIdAsync(1, default);
        var many = await grain.GetByIdsAsync([1], default);
        var create = await grain.CreateAsync(request, default);
        var update = await grain.UpdateAsync(request, default);
        var delete = await grain.DeleteAsync(request, default);

        await Assert.That(new[] { get, many, create, update, delete }.All(x => !string.IsNullOrWhiteSpace(x.error.Message))).IsTrue();
        scopes.DidNotReceive().CreateScope();
    }

    [Test]
    public async Task Content_type_grain_disposes_async_only_scoped_dependencies_asynchronously()
    {
        AsyncOnlyScopedDependency? dependency = null;
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService
            .GetAllAsync(42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>>(
                new Result<IReadOnlyList<ContentTypeDefinition>, AeroError>.Ok([])));

        var services = new ServiceCollection();
        services.AddScoped(_ => dependency = new AsyncOnlyScopedDependency());
        services.AddScoped<IContentTypeService>(provider =>
        {
            _ = provider.GetRequiredService<AsyncOnlyScopedDependency>();
            return contentTypeService;
        });

        await using var provider = services.BuildServiceProvider();
        var grain = new AeroContentTypeGrain(
            NullLogger<AeroActor>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        var result = await grain.GetAllAsync(42);

        await Assert.That(result).IsEmpty();
        await Assert.That(dependency).IsNotNull();
        await Assert.That(dependency!.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Content_item_scoped_operations_isolate_foreign_site_and_allow_owning_site()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentItemVersion>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            new ContentTypeDocument { Id = 10, SiteId = 1, Alias = "article", Name = "Article" });
        harness.Session.Store(
            new ContentItem
            {
                Id = 20,
                SiteId = 1,
                ContentTypeAlias = "article",
                Title = "Local",
                Slug = "local",
                Culture = "en-US"
            },
            new ContentItem
            {
                Id = 21,
                SiteId = 2,
                ContentTypeAlias = "article",
                Title = "Foreign",
                Slug = "foreign",
                Culture = "en-US"
            });
        await harness.Session.SaveChangesAsync();

        var typeService = Substitute.For<IContentTypeService>();
        typeService.GetByAliasAsync(1, "article", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(
                    new ContentTypeDefinition
                    {
                        Id = 10,
                        SiteId = 1,
                        Alias = "article",
                        Name = "Article"
                    })));
        var messageBus = Substitute.For<IMessageBus>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDocumentSession>(harness.Session);
        services.AddSingleton(typeService);
        services.AddSingleton(messageBus);
        services.AddScoped<AeroContentService>();
        services.AddScoped<IContentService>(
            provider => provider.GetRequiredService<AeroContentService>());
        services.AddScoped<ContentHierarchyValidator>();
        services.AddScoped<ContentValidationService>();
        services.AddScoped<ContentCommandService>();
        services.AddScoped<ContentEventPublisher>();

        await using var provider = services.BuildServiceProvider();
        var grain = new AeroContentItemGrain(
            NullLogger<AeroActor>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        var foreignGet = await grain.GetByIdAsync(21, 1, default);
        var foreignSave = await grain.SaveDraftAsync(
            new ContentItemViewModel
            {
                Id = 21,
                SiteId = 1,
                ContentTypeAlias = "article",
                Title = "Attacker",
                Slug = "attacker",
                Culture = "en-US",
                FieldsJson = "{}"
            },
            1,
            default);
        var foreignPublish = await grain.PublishAsync(21, 1, default);
        var foreignUnpublish = await grain.UnpublishAsync(21, 1, default);
        var foreignDelete = await grain.DeleteAsync(21, 1, default);

        await Assert.That(new[]
        {
            foreignGet.error.Message,
            foreignSave.error.Message,
            foreignPublish.error.Message,
            foreignUnpublish.error.Message,
            foreignDelete.error.Message
        }.All(message => !string.IsNullOrWhiteSpace(message))).IsTrue();

        var localGet = await grain.GetByIdAsync(20, 1, default);
        var localSave = await grain.SaveDraftAsync(
            new ContentItemViewModel
            {
                Id = 20,
                SiteId = 999,
                ContentTypeAlias = "attacker",
                Title = "Updated",
                Slug = "updated",
                Culture = "en-US",
                FieldsJson = "{}"
            },
            1,
            default);
        var localPublish = await grain.PublishAsync(20, 1, default);
        var localUnpublish = await grain.UnpublishAsync(20, 1, default);
        var localDelete = await grain.DeleteAsync(20, 1, default);

        await Assert.That(new[]
        {
            localGet.error.Message,
            localSave.error.Message,
            localPublish.error.Message,
            localUnpublish.error.Message,
            localDelete.error.Message
        }.All(string.IsNullOrWhiteSpace)).IsTrue();
        await Assert.That(localSave.data.SiteId).IsEqualTo(1);
        await Assert.That(localSave.data.ContentTypeAlias).IsEqualTo("article");

        await using var verify = await harness.Store.QuerySessionAsync();
        var foreign = await verify.LoadAsync<ContentItem>(21);
        await Assert.That(foreign).IsNotNull();
        await Assert.That(foreign!.SiteId).IsEqualTo(2);
        await Assert.That(foreign.Title).IsEqualTo("Foreign");
        await Assert.That(await verify.LoadAsync<ContentItem>(20)).IsNull();
    }

    private sealed class AsyncOnlyScopedDependency : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
