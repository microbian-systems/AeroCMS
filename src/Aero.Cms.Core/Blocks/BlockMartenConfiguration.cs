using Aero.Cms.Abstractions.Blocks;
using Marten;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Central Marten configuration for CMS block polymorphic serialization.
/// </summary>
public sealed class BlockMartenConfiguration : IConfigureMarten
{
    public void Configure(IServiceProvider services, StoreOptions options)
    {
        var mappedTypes = GeneratedBlockModelManifest.Blocks.Values
            .Select(descriptor => new MappedType(descriptor.ModelType, descriptor.BlockType))
            .ToArray();

        options.Schema.For<BlockBase>().AddSubClassHierarchy(mappedTypes);
    }
}
