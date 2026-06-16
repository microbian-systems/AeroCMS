using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Modules.Pages;

public interface IEditorBlockMapper
{
    List<BlockBase> MapBlocks(IReadOnlyList<EditorBlock> editorBlocks);

    BlockBase? MapBlock(EditorBlock editorBlock);
}

/// <summary>
/// Translates transient editor DTOs into persisted/renderable block models.
/// Depends on the page-editor definition registry so new packages can extend
/// mapping behavior through catalog definitions instead of central switches.
/// </summary>
public sealed class EditorBlockMapper(
    IPageEditorDefinitionRegistry definitionRegistry) : IEditorBlockMapper
{
    public List<BlockBase> MapBlocks(IReadOnlyList<EditorBlock> editorBlocks)
    {
        ArgumentNullException.ThrowIfNull(editorBlocks);

        return editorBlocks
            .Select(MapBlock)
            .OfType<BlockBase>()
            .ToList();
    }

    public BlockBase? MapBlock(EditorBlock editorBlock)
    {
        ArgumentNullException.ThrowIfNull(editorBlock);

        if (editorBlock.CompositionNodes.Count > 0)
        {
            return new NeoCompositionBlock
            {
                ResponsiveStyle = editorBlock.Style.DeepClone(),
                Nodes = editorBlock.CompositionNodes
                    .Select(node => EditorNodeMemento.Capture(node).Restore())
                    .ToList()
            };
        }

        if (definitionRegistry.TryGetDescriptor(editorBlock.Type, out var registeredDescriptor) &&
            registeredDescriptor.LegacyDefinition is { } definition)
        {
            var registeredBlock = definition.ToBlockBase(editorBlock);
            if (registeredBlock is null)
            {
                return null;
            }

            registeredBlock.ResponsiveStyle = editorBlock.Style.DeepClone();
            return registeredBlock;
        }

        if (definitionRegistry.TryGetDescriptor(editorBlock.Type, out var descriptor) &&
            descriptor.LegacyDefinition is null)
        {
            return new NeoCompositionBlock
            {
                ResponsiveStyle = editorBlock.Style.DeepClone(),
                Nodes = editorBlock.CompositionNodes
                    .Select(node => EditorNodeMemento.Capture(node).Restore())
                    .ToList()
            };
        }

        return null;
    }
}
