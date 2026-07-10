using System.Collections.ObjectModel;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Provides metadata for compiled CMS block renderers. Source generation populates the manifest when available.
/// </summary>
public static partial class CmsBlockManifest
{
        /// <summary>
    /// Blocks.
    /// </summary>
public static readonly IReadOnlyDictionary<string, CmsBlockDescriptor> Blocks = CreateBlocks();

        /// <summary>
    /// TryGet method.
    /// </summary>
public static bool TryGet(string blockType, out CmsBlockDescriptor descriptor)
        => Blocks.TryGetValue(blockType, out descriptor!);

    private static IReadOnlyDictionary<string, CmsBlockDescriptor> CreateBlocks()
    {
        var blocks = new Dictionary<string, CmsBlockDescriptor>(StringComparer.OrdinalIgnoreCase);
        Populate(blocks);
        return new ReadOnlyDictionary<string, CmsBlockDescriptor>(blocks);
    }

    static partial void Populate(Dictionary<string, CmsBlockDescriptor> blocks);
}

/// <summary>
/// Provides block renderer adapter lookup. Source generation populates the adapter table when available.
/// </summary>
public static partial class CmsBlockRenderRegistry
{
    private static readonly IReadOnlyDictionary<string, ICmsBlockRenderAdapter> Adapters = CreateAdapters();

        /// <summary>
    /// TryGet method.
    /// </summary>
public static bool TryGet(string blockType, out ICmsBlockRenderAdapter adapter)
        => Adapters.TryGetValue(blockType, out adapter!);

    private static IReadOnlyDictionary<string, ICmsBlockRenderAdapter> CreateAdapters()
    {
        var adapters = new Dictionary<string, ICmsBlockRenderAdapter>(StringComparer.OrdinalIgnoreCase);
        PopulateAdapters(adapters);
        return new ReadOnlyDictionary<string, ICmsBlockRenderAdapter>(adapters);
    }

    static partial void PopulateAdapters(Dictionary<string, ICmsBlockRenderAdapter> adapters);
}

/// <summary>
/// DI-friendly wrapper for the source-generated block renderer adapter lookup.
/// </summary>
public sealed class GeneratedCmsBlockRenderRegistry : ICmsBlockRenderRegistry
{
        /// <summary>
    /// TryGet method.
    /// </summary>
public bool TryGet(string blockType, out ICmsBlockRenderAdapter adapter)
        => CmsBlockRenderRegistry.TryGet(blockType, out adapter);
}
