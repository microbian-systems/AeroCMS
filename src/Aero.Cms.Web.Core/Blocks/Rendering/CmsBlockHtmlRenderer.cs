using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Shared.Blocks.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Web.Core.Blocks.Rendering;

/// <summary>
/// Renders CMS blocks to static HTML through the generated Blazor block renderer pipeline.
/// </summary>
public sealed class CmsBlockHtmlRenderer(HtmlRenderer htmlRenderer)
{
    public async Task<IHtmlContent> RenderAsync(
        BlockBase block,
        BlockRenderContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(block);

        cancellationToken.ThrowIfCancellationRequested();

        var renderContext = context ?? new BlockRenderContext();
        var parameters = ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                ["Block"] = block,
                ["Navigation"] = renderContext.Navigation
            });

        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await htmlRenderer.RenderComponentAsync<BlockRenderer>(parameters);
            return output.ToHtmlString();
        });

        cancellationToken.ThrowIfCancellationRequested();

        return new HtmlString(html);
    }
}
