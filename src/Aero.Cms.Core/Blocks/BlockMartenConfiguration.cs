using Aero.Cms.Abstractions.Blocks;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Central Marten configuration for CMS block polymorphic serialization.
/// </summary>
public sealed class BlockMartenConfiguration : IConfigureMarten
{
    public void Configure(IServiceProvider services, StoreOptions options)
    {
        var generatedTypes = GeneratedBlockModelManifest.Blocks.Values
            .Select(descriptor => new CmsBlockModelRegistration(descriptor.BlockType, descriptor.ModelType));

        var providedTypes = services.GetServices<ICmsBlockModelProvider>()
            .SelectMany(provider => provider.GetBlockModels());

        var mappedTypes = generatedTypes
            .Concat(providedTypes)
            .GroupBy(registration => registration.BlockType, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .Select(registration => new MappedType(registration.ModelType, registration.BlockType))
            .ToArray();

        options.Schema.For<BlockBase>().AddSubClassHierarchy(mappedTypes);
    }
}
