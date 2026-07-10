using System.Text;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
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
        /// <summary>
    /// RenderAsync method.
    /// </summary>
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

        /// <summary>
    /// RenderBlocksAsync method.
    /// </summary>
public async Task<IHtmlContent> RenderBlocksAsync(
        IEnumerable<BlockBase> blocks,
        BlockRenderContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var sb = new StringBuilder();

        foreach (var block in blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blockHtml = await RenderAsync(block, context, cancellationToken);
            using var writer = new StringWriter(sb);
            blockHtml.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        }

        return new HtmlString(sb.ToString());
    }

        /// <summary>
    /// RenderRegionsAsync method.
    /// </summary>
public async Task<IHtmlContent> RenderRegionsAsync(
        IEnumerable<LayoutRegion> regions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(regions);

        var sb = new StringBuilder();

        foreach (var region in regions.OrderBy(r => r.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parameters = ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    ["Region"] = region
                });

            var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await htmlRenderer.RenderComponentAsync<LayoutRegionRenderer>(parameters);
                return output.ToHtmlString();
            });

            sb.Append(html);
        }

        return new HtmlString(sb.ToString());
    }
}
