using System.Reflection;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Blocks.Dynamic;
using AeroDB;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Central AeroDB configuration for CMS block polymorphic serialization.
/// </summary>
public sealed class BlockAeroDbConfiguration : IConfigureAeroDB
{
    private static readonly MethodInfo AddSubClassMethod = typeof(DocumentMapping<BlockBase>)
        .GetMethod(nameof(DocumentMapping<BlockBase>.AddSubClass), Type.EmptyTypes)
        ?? throw new InvalidOperationException("Could not resolve DocumentMapping<BlockBase>.AddSubClass method.");

    private static void RegisterSubClass(DocumentMapping<BlockBase> mapping, Type modelType)
    {
        AddSubClassMethod.MakeGenericMethod(modelType).Invoke(mapping, null);
    }

    public void Configure(StoreOptions options)
    {
        var generatedTypes = GeneratedBlockModelManifest.Blocks.Values
            .Select(descriptor => new CmsBlockModelRegistration(descriptor.BlockType, descriptor.ModelType));

        var mapping = options.Schema.For<BlockBase>();
        foreach (var registration in generatedTypes)
            RegisterSubClass(mapping, registration.ModelType);

        options.Schema.For<DynamicBlockDefinition>()
            .Index(x => x.ContentTypeId)
            .Index(x => x.SiteId);
    }

    public void Configure(IServiceProvider? services, StoreOptions options)
    {
        if (services is null)
        {
            Configure(options);
            return;
        }

        var generatedTypes = GeneratedBlockModelManifest.Blocks.Values
            .Select(descriptor => new CmsBlockModelRegistration(descriptor.BlockType, descriptor.ModelType));

        var providedTypes = services.GetServices<ICmsBlockModelProvider>()
            .SelectMany(provider => provider.GetBlockModels());

        var mergedRegistrations = generatedTypes
            .Concat(providedTypes)
            .GroupBy(registration => registration.BlockType, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last());

        var mapping = options.Schema.For<BlockBase>();
        foreach (var registration in mergedRegistrations)
            RegisterSubClass(mapping, registration.ModelType);

        options.Schema.For<DynamicBlockDefinition>()
            .Index(x => x.ContentTypeId)
            .Index(x => x.SiteId);
    }
}
