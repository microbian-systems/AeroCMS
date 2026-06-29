using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Runtime adapter for component-backed block renderers. Source-generated adapters
/// remain preferred, but this keeps package-provided renderers deterministic when a
/// registry is registered through DI.
/// </summary>
public sealed class ComponentCmsBlockRenderAdapter<TBlock, TComponent> : ICmsBlockRenderAdapter<TBlock>
    where TBlock : BlockBase
    where TComponent : IComponent
{
    public ComponentCmsBlockRenderAdapter(string blockType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);

        BlockType = blockType;
    }

    public string BlockType { get; }

    public Type ModelType => typeof(TBlock);

    public RenderFragment Render(TBlock block, BlockRenderContext context) => builder =>
    {
        builder.OpenComponent<TComponent>(0);
        builder.AddAttribute(1, "Block", block);
        builder.CloseComponent();
    };

    public RenderFragment Render(IBlock block, BlockRenderContext context)
    {
        if (block is not TBlock typedBlock)
        {
            return builder => { };
        }

        return Render(typedBlock, context);
    }
}
