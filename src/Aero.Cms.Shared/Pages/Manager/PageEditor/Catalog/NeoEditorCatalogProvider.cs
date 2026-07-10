using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

/// <summary>
/// Represents a class for NeoEditorCatalogProvider.
/// </summary>
public partial class NeoEditorCatalogProvider : INeoEditorCatalogProvider
{
    private readonly Dictionary<string, NeoEditorCatalogItem> _items;

    /// <summary>
    /// Declared here, implemented by the source generator
    /// (GeneratedNeoEditorCatalog.g.cs) on server builds. When the generator does
    /// not emit (WASM client builds), this partial declaration is silently removed
    /// by the compiler — the catalog starts empty and the editor shell loads without
    /// crashing. Catalog items can be fetched via a future client-side API endpoint.
    /// </summary>
    partial void PopulateGeneratedCatalog(List<NeoEditorCatalogItem> items);

        /// <summary>
    /// Initializes a new instance of the <see cref="NeoEditorCatalogProvider"/> class.
    /// </summary>
public NeoEditorCatalogProvider(IPageEditorDefinitionRegistry? definitionRegistry = null)
    {
        var items = new List<NeoEditorCatalogItem>();
        PopulateGeneratedCatalog(items);

        _items = new Dictionary<string, NeoEditorCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
            _items[item.CatalogId] = item;

        // Union registry items last so they overwrite source-generated items
        if (definitionRegistry is not null)
        {
            foreach (var descriptor in definitionRegistry.AllDescriptors)
            {
                var catalogItem = ToCatalogItem(descriptor);
                _items[catalogItem.CatalogId] = catalogItem;
            }

            foreach (var legacy in definitionRegistry.LegacyDefinitions)
            {
                var catalogItem = ToCatalogItem(legacy);
                _items[catalogItem.CatalogId] = catalogItem;
            }
        }
    }

        /// <summary>
    /// GetCatalogItems method.
    /// </summary>
public IReadOnlyList<NeoEditorCatalogItem> GetCatalogItems() => _items.Values.ToList();

        /// <summary>
    /// TryGet method.
    /// </summary>
public bool TryGet(string catalogId, out NeoEditorCatalogItem item) =>
        _items.TryGetValue(catalogId, out item!);

    private static NeoEditorCatalogItem ToCatalogItem(PageEditorDefinitionDescriptor definition) =>
        new()
        {
            CatalogId = definition.CatalogId,
            DisplayName = definition.Catalog.DisplayName,
            Description = definition.Catalog.Description,
            Section = ToCatalogSection(definition.Catalog.Category),
            Kind = ToCatalogKind(definition.Catalog.Kind),
            SortOrder = definition.Catalog.SortOrder,
            IconName = definition.Catalog.IconName,
            AllowChildren = definition.Catalog.Composition.CanContainChildren,
            PublicStaticSsrSafe = definition.Catalog.PublicStaticSsrSafe,
            EditorPreviewComponentType = definition.Catalog.PreviewComponentType,
            PropertyEditorComponentType = definition.Catalog.PropertyEditorComponentType
        };

    private static NeoEditorCatalogItem ToCatalogItem(IPageEditorBlockDefinition definition) =>
        new()
        {
            CatalogId = definition.CatalogId,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Section = ToCatalogSection(definition.Category),
            Kind = ToCatalogKind(definition.Kind),
            SortOrder = definition.SortOrder,
            IconName = definition.IconName,
            PublicStaticSsrSafe = definition.PublicStaticSsrSafe,
            EditorPreviewComponentType = definition.PreviewComponentType,
            PropertyEditorComponentType = definition.PropertyEditorComponentType
        };

    private static NeoEditorCatalogSection ToCatalogSection(string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "aero ui" or "aeroui" or "aero" => NeoEditorCatalogSection.AeroUi,
            "primitive" or "primitives" => NeoEditorCatalogSection.Primitives,
            "component" or "components" => NeoEditorCatalogSection.Components,
            "hyper" or "hyperui" or "hyper ui" => NeoEditorCatalogSection.Hyper,
            "neo" or "neoui" or "neo ui" => NeoEditorCatalogSection.Neo,
            _ => NeoEditorCatalogSection.AeroUi
        };

    private static NeoEditorCatalogKind ToCatalogKind(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            "primitive" => NeoEditorCatalogKind.Primitive,
            "component" => NeoEditorCatalogKind.Component,
            _ => NeoEditorCatalogKind.Block
        };

    private static NeoEditorCatalogKind ToCatalogKind(NeoPageNodeKind kind) =>
        kind switch
        {
            NeoPageNodeKind.Primitive => NeoEditorCatalogKind.Primitive,
            NeoPageNodeKind.Component or NeoPageNodeKind.Container or NeoPageNodeKind.Section =>
                NeoEditorCatalogKind.Component,
            _ => NeoEditorCatalogKind.Block
        };
}
