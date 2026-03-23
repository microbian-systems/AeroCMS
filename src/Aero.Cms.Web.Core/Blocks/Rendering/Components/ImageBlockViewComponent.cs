using System.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Aero.Cms.Core.Blocks;

namespace Aero.Cms.Core.Web.Blocks.Rendering.Components;

/// <summary>
/// ViewComponent for rendering image blocks with optional caption and alt text.
/// </summary>
/// <remarks>
/// This component renders an image from an <see cref="ImageBlock"/> within a semantic figure element
/// with an optional figcaption. The MediaId is used to construct the image source URL.
/// </remarks>
public class ImageBlockViewComponent : ViewComponent
{
    /// <summary>
    /// Invokes the view component asynchronously to render the image block.
    /// </summary>
    /// <param name="block">The image block to render. Must be of type <see cref="ImageBlock"/>.</param>
    /// <returns>An <see cref="IHtmlContent"/> containing the rendered HTML figure element with image and optional caption.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the block is not an <see cref="ImageBlock"/>.</exception>
    public Task<IHtmlContent> InvokeAsync(BlockBase block)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (block is not ImageBlock imageBlock)
        {
            throw new InvalidOperationException($"Expected block of type {nameof(ImageBlock)} but received {block.GetType().Name}");
        }

        var altText = HttpUtility.HtmlEncode(imageBlock.AltText ?? string.Empty);
        var imageUrl = $"/api/media/{imageBlock.MediaId}";

        var html = new System.Text.StringBuilder();
        html.Append("""<figure class="my-6">""");
        html.Append($"""<img src="{imageUrl}" alt="{altText}" class="w-full h-auto rounded-lg shadow-md" loading="lazy" />""");

        if (!string.IsNullOrWhiteSpace(imageBlock.Caption))
        {
            var escapedCaption = HttpUtility.HtmlEncode(imageBlock.Caption);
            html.Append($"""<figcaption class="mt-2 text-sm text-gray-600 text-center italic">{escapedCaption}</figcaption>""");
        }

        html.Append("""</figure>""");

        return Task.FromResult<IHtmlContent>(new HtmlString(html.ToString()));
    }
}
