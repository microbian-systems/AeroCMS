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
        /// <summary>
    /// Initializes a new instance of the <see cref="ComponentCmsBlockRenderAdapter"/> class.
    /// </summary>
public ComponentCmsBlockRenderAdapter(string blockType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);

        BlockType = blockType;
    }

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public string BlockType { get; }

        /// <summary>
    /// Gets or sets the Model Type.
    /// </summary>
public Type ModelType => typeof(TBlock);

        /// <summary>
    /// Render method.
    /// </summary>
public RenderFragment Render(TBlock block, BlockRenderContext context) => builder =>
    {
        builder.OpenComponent<TComponent>(0);
        builder.AddAttribute(1, "Block", block);
        builder.CloseComponent();
    };

        /// <summary>
    /// Render method.
    /// </summary>
public RenderFragment Render(IBlock block, BlockRenderContext context)
    {
        if (block is not TBlock typedBlock)
        {
            return builder => { };
        }

        return Render(typedBlock, context);
    }
}
