using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Web.Core.Blocks.Rendering;

/// <summary>
/// A singleton registry that manages block slice renderers and implements the visitor pattern
/// for dispatching to the appropriate renderer based on block type.
/// </summary>
public sealed class BlockSliceRegistry : IBlockVisitor
{
    private readonly Dictionary<Type, IBlockSliceRenderer> _renderers = new();
    private readonly IBlockSliceRenderer? _defaultRenderer;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="BlockSliceRegistry"/>.
    /// </summary>
    /// <param name="defaultRenderer">Optional default renderer for unregistered block types.</param>
    public BlockSliceRegistry(IBlockSliceRenderer? defaultRenderer = null)
    {
        _defaultRenderer = defaultRenderer;
    }

    /// <summary>
    /// Registers a renderer for a specific block type.
    /// </summary>
    /// <param name="renderer">The renderer to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderer"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a renderer is already registered for the block type.</exception>
    public void Register(IBlockSliceRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        lock (_lock)
        {
            if (_renderers.ContainsKey(renderer.BlockType))
            {
                throw new InvalidOperationException(
                    $"A renderer for block type '{renderer.BlockType.Name}' is already registered.");
            }

            _renderers[renderer.BlockType] = renderer;
        }
    }

    /// <summary>
    /// Registers a renderer for a block type, replacing any existing renderer.
    /// </summary>
    /// <param name="renderer">The renderer to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderer"/> is null.</exception>
    public void RegisterOverride(IBlockSliceRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        lock (_lock)
        {
            _renderers[renderer.BlockType] = renderer;
        }
    }

    /// <summary>
    /// Resolves the renderer for a specific block type.
    /// </summary>
    /// <param name="blockType">The block type to resolve.</param>
    /// <returns>The registered renderer, or the default renderer if available.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no renderer is found and no default is available.</exception>
    public IBlockSliceRenderer Resolve(Type blockType)
    {
        lock (_lock)
        {
            if (_renderers.TryGetValue(blockType, out var renderer))
            {
                return renderer;
            }

            // Search for renderer based on inheritance hierarchy
            foreach (var kvp in _renderers)
            {
                if (kvp.Key.IsAssignableFrom(blockType))
                {
                    return kvp.Value;
                }
            }

            if (_defaultRenderer != null)
            {
                return _defaultRenderer;
            }

            throw new InvalidOperationException(
                $"No renderer found for block type '{blockType.Name}' and no default renderer is configured.");
        }
    }

    /// <summary>
    /// Visits a block and returns the rendered HTML content.
    /// </summary>
    /// <param name="block">The block to visit.</param>
    /// <returns>The rendered HTML content.</returns>
    public IHtmlContent Visit(BlockBase block)
    {
        ArgumentNullException.ThrowIfNull(block);

        var renderer = Resolve(block.GetType());
        return renderer.Render(block);
    }
}
