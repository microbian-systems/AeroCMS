using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Web.Core.Blocks.Rendering;

/// <summary>
/// Legacy slice renderer bridge that delegates block rendering to the generated Blazor pipeline.
/// </summary>
public sealed class CmsBlockSliceRenderer(CmsBlockHtmlRenderer htmlRenderer) : IBlockSliceRenderer
{
    public Type BlockType => typeof(BlockBase);

    public IHtmlContent Render(BlockBase block)
        => htmlRenderer.RenderAsync(block).GetAwaiter().GetResult();
}
