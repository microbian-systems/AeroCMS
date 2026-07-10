using System.Collections.ObjectModel;

namespace Aero.Cms.Abstractions.Blocks;

/// <summary>
/// Describes a source-generated CMS block model registration.
/// </summary>
public readonly record struct GeneratedBlockModelDescriptor(
    string BlockType,
    string DisplayName,
    string? Description,
    string? Category,
    string? Icon,
    int SortOrder,
    int SchemaVersion,
    Type ModelType);

/// <summary>
/// Provides metadata for CMS block models. Source generation populates the manifest when available.
/// </summary>
public static partial class GeneratedBlockModelManifest
{
        /// <summary>
    /// Blocks.
    /// </summary>
public static readonly IReadOnlyDictionary<string, GeneratedBlockModelDescriptor> Blocks = CreateBlocks();

        /// <summary>
    /// ModelTypes.
    /// </summary>
public static readonly Type[] ModelTypes = Blocks.Values
        .Select(static descriptor => descriptor.ModelType)
        .ToArray();

        /// <summary>
    /// TryGet method.
    /// </summary>
public static bool TryGet(string blockType, out GeneratedBlockModelDescriptor descriptor)
        => Blocks.TryGetValue(blockType, out descriptor);

    private static IReadOnlyDictionary<string, GeneratedBlockModelDescriptor> CreateBlocks()
    {
        var blocks = new Dictionary<string, GeneratedBlockModelDescriptor>(StringComparer.OrdinalIgnoreCase);
        Populate(blocks);
        return new ReadOnlyDictionary<string, GeneratedBlockModelDescriptor>(blocks);
    }

    static partial void Populate(Dictionary<string, GeneratedBlockModelDescriptor> blocks);
}
