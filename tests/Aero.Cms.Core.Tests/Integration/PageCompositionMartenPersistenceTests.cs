using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using JasperFx.Events;
using Marten;
using Marten.Schema;
using MysticMind.PostgresEmbed;
using Npgsql;
using NSubstitute;
using System.Text.Json;
using Wolverine;
using ProjectionLifecycle = JasperFx.Events.Projections.ProjectionLifecycle;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PageCompositionMartenPersistenceTests
{
    private static readonly SemaphoreSlim InitializationLock = new(1, 1);
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
    public async Task PostgresRoundTripPreservesMixedResponsiveComposition()
    {
        await EnsureInitializedAsync();
        await s_store!.Advanced.Clean.CompletelyRemoveAllAsync();

        var page = CreatePage();
        await using (var session = s_store.LightweightSession())
        {
            session.Store(page);
            await session.SaveChangesAsync();
        }

        await using var query = s_store.QuerySession();
        var restored = await query.LoadAsync<PageDocument>(page.Id);

        restored.Should().NotBeNull();
        restored!.Culture.Should().Be("ar-SA");
        restored.PublicationState.Should().Be(ContentPublicationState.Published);
        restored.RootNodes.Should().HaveCount(2);
        restored.RootNodes[0].CatalogId.Should().Be("hero");

        var root = restored.RootNodes[1].Children.Should()
            .ContainSingle()
            .Subject;
        root.Style.Base.Direction.Should().Be(ContentDirection.RightToLeft);
        root.Style.Base.BackgroundImage!.Repeat
            .Should().Be(BackgroundImageRepeat.RepeatX);
        root.Style.Tablet!.Padding!.InlineStart.Should().Be(
            new CssLength(16, CssLengthUnit.Pixels));
        root.Style.Mobile!.Hidden.Should().BeTrue();
        root.Children.Should().ContainSingle()
            .Which.Properties["text"].GetString().Should().Be("مرحبا");
    }

    [Test]
    public async Task PublishProjectsDraftCompositionIntoPublishedLayout()
    {
        await EnsureInitializedAsync();
        await s_store!.Advanced.Clean.CompletelyRemoveAllAsync();

        const long pageId = 602;
        const long blockId = 702;
        var page = CreatePage();
        page.Id = pageId;
        page.TranslationGroupId = pageId;
        page.PublicationState = ContentPublicationState.Draft;
        page.PublishedVersion = 3;
        page.LayoutRegions = [];
        var block = new NeoCompositionBlock
        {
            Id = blockId,
            Nodes = page.RootNodes[1].Children
        };
        var editor = new PageEditorState
        {
            Id = pageId,
            SiteId = page.SiteId,
            DraftVersion = 4,
            Blocks =
            [
                new EditorBlockPlacement
                {
                    ClientId = "composition",
                    BlockId = blockId,
                    Region = "main",
                    Order = 0
                }
            ]
        };

        await using (var seed = s_store.LightweightSession())
        {
            seed.Store(page);
            seed.Store(editor);
            seed.Store<BlockBase>(block);
            await seed.SaveChangesAsync();
        }

        var bus = Substitute.For<IMessageBus>();
        await using (var publishSession = s_store.LightweightSession())
        {
            var service = new PagePublishingWorkflowService(
                publishSession,
                bus,
                new PageLayoutManifestBuilder(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PagePublishingWorkflowService>.Instance);

            var result = await service.PublishNowAsync(pageId);
            result.Should().BeOfType<Result<bool, AeroError>.Ok>()
                .Which.Value.Should().BeTrue();
        }

        await using var query = s_store.QuerySession();
        var published = await query.LoadAsync<PageDocument>(pageId);

        published.Should().NotBeNull();
        published!.PublicationState.Should().Be(
            ContentPublicationState.Published);
        published.PublishedVersion.Should().Be(4);
        published.LayoutRegions.Should().ContainSingle()
            .Which.Columns.Should().ContainSingle()
            .Which.Blocks.Should().ContainSingle()
            .Which.Should().Match<BlockPlacement>(placement =>
                placement.BlockId == blockId &&
                placement.BlockType == "neo_composition");
        await bus.Received(1).PublishAsync(
            Arg.Is<object>(message =>
                message.GetType() == typeof(PageViewModelUpdated)));
        await bus.Received(1).PublishAsync(
            Arg.Is<object>(message =>
                message.GetType() == typeof(PageContentUpdatedEvent) &&
                ((PageContentUpdatedEvent)message).ContentId == pageId));
    }

    private static PageDocument CreatePage() =>
        new()
        {
            Id = 601,
            SiteId = 42,
            TranslationGroupId = 601,
            Culture = "ar-SA",
            Title = "Database mixed page",
            Slug = "database-mixed-page",
            Path = "/database-mixed-page",
            PublicationState = ContentPublicationState.Published,
            PublishedVersion = 4,
            RootNodes =
            [
                new NeoPageNode
                {
                    NodeId = "hero",
                    CatalogId = "hero",
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["mainText"] = JsonSerializer.SerializeToElement("Canned hero")
                    }
                },
                new NeoPageNode
                {
                    NodeId = "composition",
                    CatalogId = "neo.composition",
                    Children =
                    [
                        new NeoPageNode
                        {
                            NodeId = "container",
                            CatalogId = "primitive.container",
                            Kind = NeoPageNodeKind.Container,
                            Style = new ResponsiveNodeStyle
                            {
                                Base = new NodeStyle
                                {
                                    Direction = ContentDirection.RightToLeft,
                                    BackgroundImage = new BackgroundImageStyle
                                    {
                                        Url = "/media/pattern.png",
                                        Repeat = BackgroundImageRepeat.RepeatX
                                    }
                                },
                                Tablet = new NodeStyleOverride
                                {
                                    Padding = new LogicalSpacingOverride
                                    {
                                        InlineStart = new CssLength(
                                            16,
                                            CssLengthUnit.Pixels)
                                    }
                                },
                                Mobile = new NodeStyleOverride { Hidden = true }
                            },
                            Children =
                            [
                                new NeoPageNode
                                {
                                    NodeId = "text",
                                    CatalogId = "primitive.text",
                                    Kind = NeoPageNodeKind.Primitive,
                                    Properties = new Dictionary<string, JsonElement>
                                    {
                                        ["text"] =
                                            JsonSerializer.SerializeToElement(
                                                "مرحبا")
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        };

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

            const string databaseName = "aero_page_composition";
            s_postgres = new PgServer(
                "18.3.0",
                databaseName,
                port: 5435,
                clearInstanceDirOnStop: true);
            await s_postgres.StartAsync();
            await EnsureDatabaseAsync(databaseName, s_postgres.PgPort);

            s_store = DocumentStore.For(options =>
            {
                options.Connection(
                    $"Host=localhost;Port={s_postgres.PgPort};Username={databaseName};Database={databaseName};");
                options.DatabaseSchemaName = "public";
                options.Events.StreamIdentity = StreamIdentity.AsString;
                options.Schema.For<PageDocument>().Identity(page => page.Id);
                options.Schema.For<PageDocument>().Index(page => page.SiteId);
                options.Schema.For<PageDocument>().Index(page => page.Culture);
                options.Schema.For<PageEditorState>()
                    .Identity(editor => editor.Id);
                options.Schema.For<BlockBase>().AddSubClassHierarchy(
                    new MappedType(
                        typeof(NeoCompositionBlock),
                        "neo_composition"));
                options.Projections.Add(
                    new PageDocumentProjection(),
                    ProjectionLifecycle.Inline);
            });
        }
        finally
        {
            InitializationLock.Release();
        }
    }

    private static async Task EnsureDatabaseAsync(string databaseName, int port)
    {
        await using var connection = new NpgsqlConnection(
            $"Host=localhost;Port={port};Username={databaseName};Database=postgres;");
        await connection.OpenAsync();

        await using var existsCommand = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @databaseName)",
            connection);
        existsCommand.Parameters.AddWithValue("databaseName", databaseName);
        if (await existsCommand.ExecuteScalarAsync() is true)
        {
            return;
        }

        await using var createCommand = new NpgsqlCommand(
            $"CREATE DATABASE {databaseName} OWNER {databaseName}",
            connection);
        await createCommand.ExecuteNonQueryAsync();
    }
}
