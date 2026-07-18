using Aero.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Grains;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentGrainScopeTests
{
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
