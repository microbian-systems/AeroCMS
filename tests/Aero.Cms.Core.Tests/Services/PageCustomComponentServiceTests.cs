using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages.CustomComponents;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Marten;
using MysticMind.PostgresEmbed;
using NSubstitute;
using Npgsql;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PageCustomComponentServiceTests
{
    private static readonly SemaphoreSlim InitializationLock = new(1, 1);
    private static readonly SemaphoreSlim TestLock = new(1, 1);
    private static PgServer? s_postgres;
    private static IDocumentStore? s_store;

    [After(Class)]
    public static async Task CleanupAsync()
    {
        s_store?.Dispose();
        s_store = null;

        if (s_postgres is not null)
        {
            await s_postgres.StopAsync();
            await s_postgres.DisposeAsync();
            s_postgres = null;
        }
    }

    [Test]
    public async Task Same_name_conflicts_only_within_the_current_site()
    {
        await TestLock.WaitAsync();
        try
        {
            await using var session = await OpenCleanSessionAsync();
            session.Store(
                Component(100, 42, "Feature Card"),
                Component(101, 84, "Feature Card"));
            await session.SaveChangesAsync();

            var service = CreateService(session, 42);
            var result = await service.SaveAsync(Request("Feature Card"));

            await Assert.That(result).IsTypeOf<Result<PageCustomComponent, AeroError>.Failure>();
            if (result is Result<PageCustomComponent, AeroError>.Failure failure)
            {
                await Assert.That(failure.Error).IsTypeOf<AeroError.Conflict>();
            }
        }
        finally
        {
            TestLock.Release();
        }
    }

    [Test]
    public async Task Same_name_is_allowed_on_a_different_site()
    {
        await TestLock.WaitAsync();
        try
        {
            await using var session = await OpenCleanSessionAsync();
            session.Store(Component(100, 42, "Feature Card"));
            await session.SaveChangesAsync();

            var service = CreateService(session, 84);
            var result = await service.SaveAsync(Request("Feature Card"));

            await Assert.That(result.IsSuccess).IsTrue();
            if (result is Result<PageCustomComponent, AeroError>.Ok ok)
            {
                await Assert.That(ok.Value.SiteId).IsEqualTo(84);
            }
        }
        finally
        {
            TestLock.Release();
        }
    }

    [Test]
    public async Task Read_instance_update_and_delete_are_tenant_isolated()
    {
        await TestLock.WaitAsync();
        try
        {
            await using var session = await OpenCleanSessionAsync();
            session.Store(
                Component(100, 42, "Site A"),
                Component(101, 84, "Site B"));
            await session.SaveChangesAsync();

            var service = CreateService(session, 42);
            var all = await service.GetAllAsync();
            var instance = await service.CreateInstanceAsync(101);
            var update = await service.UpdateAsync(101, Request("Changed"));
            var delete = await service.DeleteAsync(101);

            if (all is Result<IReadOnlyList<PageCustomComponent>, AeroError>.Ok ok)
            {
                await Assert.That(ok.Value.Count).IsEqualTo(1);
                await Assert.That(ok.Value[0].Id).IsEqualTo(100);
            }

            await Assert.That(instance.IsFailure).IsTrue();
            await Assert.That(update.IsFailure).IsTrue();
            await Assert.That(delete.IsFailure).IsTrue();
            await Assert.That(await session.LoadAsync<PageCustomComponent>(101))
                .IsNotNull();
        }
        finally
        {
            TestLock.Release();
        }
    }

    private static PageCustomComponentService CreateService(
        IDocumentSession session,
        long siteId)
    {
        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(siteId);
        return new PageCustomComponentService(
            session,
            siteContext,
            new SavePageCustomComponentRequestValidator());
    }

    private static SavePageCustomComponentRequest Request(string name) =>
        new(name, Root());

    private static PageCustomComponent Component(long id, long siteId, string name) =>
        new()
        {
            Id = id,
            SiteId = siteId,
            Name = name,
            Category = "Custom",
            Root = Root(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static NeoPageNode Root() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = "ui.card",
            Kind = NeoPageNodeKind.Component
        };

    private static async Task<IDocumentSession> OpenCleanSessionAsync()
    {
        await EnsureInitializedAsync();
        await s_store!.Advanced.Clean.CompletelyRemoveAllAsync();
        return s_store.LightweightSession();
    }

    private static async Task EnsureInitializedAsync()
    {
        if (s_store is not null)
        {
            return;
        }

        await InitializationLock.WaitAsync();
        try
        {
            if (s_store is not null)
            {
                return;
            }

            s_postgres = new PgServer(
                "18.3.0",
                "aero_custom_components",
                port: 5434,
                clearInstanceDirOnStop: true);
            await s_postgres.StartAsync();
            await EnsureDatabaseAsync(s_postgres.PgPort);

            s_store = DocumentStore.For(options =>
            {
                options.Connection(
                    $"Host=localhost;Port={s_postgres.PgPort};Username=aero_custom_components;Database=aero_custom_components;");
                options.DatabaseSchemaName = "public";
                options.Schema.For<PageCustomComponent>()
                    .Identity(component => component.Id);
                options.Schema.For<PageCustomComponent>()
                    .Index(component => component.SiteId);
                options.Schema.For<PageCustomComponent>()
                    .UniqueIndex(component => component.SiteId, component => component.Name);
            });
        }
        finally
        {
            InitializationLock.Release();
        }
    }

    private static async Task EnsureDatabaseAsync(int port)
    {
        const string databaseName = "aero_custom_components";
        var masterConnectionString =
            $"Host=localhost;Port={port};Username={databaseName};Database=postgres;";

        await using var connection = new NpgsqlConnection(masterConnectionString);
        await connection.OpenAsync();

        await using var existsCommand = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @databaseName)",
            connection);
        existsCommand.Parameters.AddWithValue("databaseName", databaseName);
        var exists = await existsCommand.ExecuteScalarAsync() is true;
        if (exists)
        {
            return;
        }

        await using var createCommand = new NpgsqlCommand(
            $"CREATE DATABASE {databaseName} OWNER {databaseName}",
            connection);
        await createCommand.ExecuteNonQueryAsync();
    }
}
