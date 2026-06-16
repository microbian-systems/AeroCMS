using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

/// <summary>
/// DI-backed, immutable page-editor definition registry.
///
/// This is the Phase 0.5 replacement for direct static registry mutation:
/// block packages register providers with DI, and this service builds the
/// authoritative descriptor lookup once from those providers.
/// </summary>
public sealed class PageEditorDefinitionRegistry : IPageEditorDefinitionRegistry
{
    private readonly IReadOnlyDictionary<string, PageEditorDefinitionDescriptor> _definitions;
    private readonly IReadOnlyCollection<PageEditorDefinitionDescriptor> _allDescriptors;
    private readonly IReadOnlyCollection<IPageEditorBlockDefinition> _legacyDefinitions;

    public PageEditorDefinitionRegistry(
        IEnumerable<IPageEditorBlockProvider> blockProviders,
        IEnumerable<IPageEditorDefinitionProvider> nativeProviders)
    {
        _definitions = BuildDefinitions(blockProviders, nativeProviders);
        _allDescriptors = _definitions.Values.ToList();
        _legacyDefinitions = _allDescriptors
            .Select(descriptor => descriptor.LegacyDefinition)
            .OfType<IPageEditorBlockDefinition>()
            .ToList();

        PageEditorBlockRegistry.UseRegistry(this);
    }

    public IReadOnlyCollection<PageEditorDefinitionDescriptor> AllDescriptors => _allDescriptors;

    public IReadOnlyCollection<IPageEditorBlockDefinition> LegacyDefinitions => _legacyDefinitions;

    public bool TryGetDescriptor(
        string? catalogId,
        out PageEditorDefinitionDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            descriptor = default!;
            return false;
        }

        return _definitions.TryGetValue(catalogId, out descriptor!);
    }

    private static IReadOnlyDictionary<string, PageEditorDefinitionDescriptor> BuildDefinitions(
        IEnumerable<IPageEditorBlockProvider> blockProviders,
        IEnumerable<IPageEditorDefinitionProvider>? nativeProviders)
    {
        ArgumentNullException.ThrowIfNull(blockProviders);

        var definitions = new Dictionary<string, PageEditorDefinitionDescriptor>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var provider in blockProviders)
        {
            foreach (var definition in provider.GetDefinitions())
            {
                var descriptor = new LegacyPageEditorDefinitionAdapter(definition)
                    .ToDescriptor();
                AddDefinition(definitions, descriptor);
            }
        }

        if (nativeProviders is not null)
        {
            foreach (var provider in nativeProviders)
            {
                foreach (var descriptor in provider.GetEditorDefinitions())
                {
                    AddDefinition(definitions, descriptor);
                }
            }
        }

        return definitions;
    }

    private static void AddDefinition(
        IDictionary<string, PageEditorDefinitionDescriptor> definitions,
        PageEditorDefinitionDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.CatalogId))
        {
            throw new InvalidOperationException(
                "Page-editor definitions must declare a non-empty catalog ID.");
        }

        if (!definitions.TryAdd(descriptor.CatalogId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate page-editor catalog ID '{descriptor.CatalogId}'. " +
                "Two providers registered the same ID with no explicit override policy. " +
                "Remove the conflicting provider or add an explicit override mechanism.");
        }
    }
}
