using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using Aero.Cms.Modules.Pages.CustomComponents;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PageCustomComponentsApiClientTests
{
    [Test]
    public async Task ClientRoundTripsCustomComponentApi()
    {
        var service = Substitute.For<IPageCustomComponentService>();
        var request = new SavePageCustomComponentRequest(
            "Feature Card",
            Node("template-root", "primitive.container"),
            "Reusable feature card",
            Tags: ["feature", "card"]);
        var created = Component(
            101,
            "Feature Card",
            request.Root,
            ["primitive.container"]);
        var updated = Component(
            101,
            "Renamed Card",
            request.Root,
            ["primitive.container"]);
        var instance = Node("instance-root", "primitive.container");

        service.SaveAsync(
                Arg.Any<SavePageCustomComponentRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Ok(created));
        service.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Ok<IReadOnlyList<PageCustomComponent>>([created]));
        service.UpdateAsync(
                101,
                Arg.Any<SavePageCustomComponentRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Ok(updated));
        service.CreateInstanceAsync(101, Arg.Any<CancellationToken>())
            .Returns(Ok(instance));
        service.DeleteAsync(101, Arg.Any<CancellationToken>())
            .Returns(Ok(true));

        await using var app = await CreateAppAsync(service);
        using var httpClient = app.GetTestClient();
        var client = new PageCustomComponentsHttpClient(
            httpClient,
            app.Services.GetRequiredService<
                ILogger<PageCustomComponentsHttpClient>>());

        var createResult = await client.CreateAsync(request);
        var createdDetail = createResult.Should()
            .BeOfType<Result<PageCustomComponentDetail, AeroError>.Ok>()
            .Subject.Value;
        createdDetail.Id.Should().Be(101);
        createdDetail.Name.Should().Be("Feature Card");
        createdDetail.Tags.Should().BeEquivalentTo("feature", "card");

        var listResult = await client.GetAllAsync();
        listResult.Should()
            .BeOfType<Result<IReadOnlyList<PageCustomComponentDetail>, AeroError>.Ok>()
            .Subject.Value.Should().ContainSingle()
            .Which.Name.Should().Be("Feature Card");

        var updateRequest = request with { Name = "Renamed Card" };
        var updateResult = await client.UpdateAsync(101, updateRequest);
        updateResult.Should()
            .BeOfType<Result<PageCustomComponentDetail, AeroError>.Ok>()
            .Subject.Value.Name.Should().Be("Renamed Card");

        var instanceResult = await client.CreateInstanceAsync(101);
        instanceResult.Should()
            .BeOfType<Result<NeoPageNode, AeroError>.Ok>()
            .Subject.Value.NodeId.Should().Be("instance-root");

        var deleteResult = await client.DeleteAsync(101);
        deleteResult.Should()
            .BeOfType<Result<bool, AeroError>.Ok>()
            .Subject.Value.Should().BeTrue();

        await service.Received(1).SaveAsync(
            Arg.Is<SavePageCustomComponentRequest>(value =>
                value.Name == "Feature Card" &&
                value.Root.NodeId == "template-root"),
            Arg.Any<CancellationToken>());
        await service.Received(1).UpdateAsync(
            101,
            Arg.Is<SavePageCustomComponentRequest>(value =>
                value.Name == "Renamed Card"),
            Arg.Any<CancellationToken>());
        await service.Received(1).CreateInstanceAsync(
            101,
            Arg.Any<CancellationToken>());
        await service.Received(1).DeleteAsync(
            101,
            Arg.Any<CancellationToken>());
    }

    private static async Task<WebApplication> CreateAppAsync(
        IPageCustomComponentService service)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(service);

        var app = builder.Build();
        app.MapPageCustomComponentsApi();
        await app.StartAsync();
        return app;
    }

    private static Result<T, AeroError> Ok<T>(T value) =>
        new Result<T, AeroError>.Ok(value);

    private static NeoPageNode Node(string nodeId, string catalogId) =>
        new()
        {
            NodeId = nodeId,
            CatalogId = catalogId,
            Kind = NeoPageNodeKind.Container
        };

    private static PageCustomComponent Component(
        long id,
        string name,
        NeoPageNode root,
        IReadOnlyList<string> referencedCatalogIds) =>
        new()
        {
            Id = id,
            SiteId = 7,
            Name = name,
            Description = "Reusable feature card",
            Category = "Custom",
            Tags = ["feature", "card"],
            Root = root,
            ReferencedCatalogIds = [.. referencedCatalogIds],
            CreatedOn = new DateTimeOffset(
                2026,
                6,
                14,
                12,
                0,
                0,
                TimeSpan.Zero),
            ModifiedOn = new DateTimeOffset(
                2026,
                6,
                14,
                12,
                30,
                0,
                0,
                TimeSpan.Zero)
        };
}
