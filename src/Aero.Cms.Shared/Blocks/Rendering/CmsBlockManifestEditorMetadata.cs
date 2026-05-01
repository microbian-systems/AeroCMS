using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editing;
using Aero.Core.Railway;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Adapts the generated block manifest to the existing editor palette metadata shape.
/// </summary>
public static class CmsBlockManifestEditorMetadata
{
    public static IReadOnlyList<BlockTypeInfo> GetAvailableBlockTypes()
    {
        return CmsBlockManifest.Blocks.Values
            .OrderBy(descriptor => descriptor.SortOrder)
            .ThenBy(descriptor => descriptor.DisplayName)
            .Select(ToBlockTypeInfo)
            .ToArray();
    }

    public static Option<BlockTypeInfo> GetBlockTypeInfo(string blockType)
    {
        return CmsBlockManifest.TryGet(blockType, out var descriptor)
            ? ToBlockTypeInfo(descriptor)
            : new Option<BlockTypeInfo>.None();
    }

    private static BlockTypeInfo ToBlockTypeInfo(CmsBlockDescriptor descriptor)
    {
        return new BlockTypeInfo
        {
            Name = descriptor.BlockType,
            DisplayName = descriptor.DisplayName,
            Description = descriptor.Description,
            Category = descriptor.Category ?? "General",
            Icon = descriptor.Icon,
            SortOrder = descriptor.SortOrder,
            Type = descriptor.ModelType
        };
    }
}
