using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Default implementation of <see cref="IPageLayoutManifestBuilder"/>.
/// Groups editor placements by region, orders them, and resolves block types
/// from the provided block dictionary.
/// </summary>
internal sealed class PageLayoutManifestBuilder : IPageLayoutManifestBuilder
{
    public Task<IReadOnlyList<LayoutRegion>> BuildAsync(
        PageEditorState? editor,
        IReadOnlyDictionary<long, BlockBase> blocks,
        CancellationToken ct = default)
    {
        if (editor?.Blocks is null || editor.Blocks.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<LayoutRegion>>([]);
        }

        var regions = editor.Blocks
            .Where(p => p.BlockId.HasValue && blocks.ContainsKey(p.BlockId.Value))
            .GroupBy(p => p.Region)
            .Select(group =>
            {
                var placements = group
                    .OrderBy(p => p.Order)
                    .Select(p => new BlockPlacement
                    {
                        BlockId = p.BlockId!.Value,
                        BlockType = blocks[p.BlockId.Value].BlockType,
                        Order = p.Order
                    })
                    .ToList();

                // Wrap placements in a single full-width column (current LayoutRegion model).
                // When the flat LayoutRegion model is adopted, this becomes a direct
                // Placements list on the region.
                var column = new LayoutColumn
                {
                    Width = 12,
                    Blocks = placements
                };

                return new LayoutRegion
                {
                    Name = group.Key,
                    Order = 0,
                    Columns = [column]
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<LayoutRegion>>(regions);
    }
}
