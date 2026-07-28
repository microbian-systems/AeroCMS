using Aero.AppServer;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Modules.Ai.Knowledge;
using AeroDB.Sable;
using FluentAssertions;
using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Web.Tests;

public sealed class WolverineSableSessionCodeGenerationTests
{
    [Test]
    public async Task AppServer_policy_compiles_handlers_with_opaque_scoped_Sable_sessions()
    {
        var builder = Host.CreateApplicationBuilder();
        WolverineOptions? configuredOptions = null;
        builder.Services.AddScoped<IDocumentSession>(
            _ => Substitute.For<IDocumentSession>());
        builder.Services.AddScoped<
            IAeroAiKnowledgeProjectionService,
            AeroAiKnowledgeProjectionService>();
        builder.Services.AddSingleton(
            Substitute.For<IContentEmbeddingGenerator>());
        builder.Services.AddWolverine(opts =>
        {
            configuredOptions = opts;
            opts.UseRuntimeCompilation();
            opts.Discovery.DisableConventionalDiscovery()
                .IncludeType<AeroAiKnowledgeProjectionHandler>();
            AeroAppServerExtensions.ConfigureSableSessionCodeGeneration(opts);
        });

        using var host = builder.Build();
        Func<Task> start = () => host.StartAsync();

        await start.Should().NotThrowAsync();
        configuredOptions.Should().NotBeNull();
        configuredOptions!
            .ServiceLocationPolicy.Should().Be(ServiceLocationPolicy.NotAllowed);
        await host.StopAsync();
    }
}
