using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Core.Blocks.Rendering.Components;

/// <summary>
/// ViewComponent for rendering rich text blocks with HTML content.
/// </summary>
/// <remarks>
/// This component renders HTML content from a <see cref="RichTextBlock"/> inside a semantic
/// container with Tailwind CSS styling.
/// </remarks>
public class RichTextBlockViewComponent : ViewComponent
{
    /// <summary>
    /// Invokes the view component asynchronously to render the rich text block.
    /// </summary>
    /// <param name="block">The rich text block to render. Must be of type <see cref="RichTextBlock"/>.</param>
    /// <returns>An <see cref="IHtmlContent"/> containing the rendered HTML.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the block is not a <see cref="RichTextBlock"/>.</exception>
    public Task<IHtmlContent> InvokeAsync(BlockBase block)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (block is not RichTextBlock richTextBlock)
        {
            throw new InvalidOperationException($"Expected block of type {nameof(RichTextBlock)} but received {block.GetType().Name}");
        }

        var html = $"""<div class="rich-text prose prose-slate max-w-none">{richTextBlock.Content}</div>""";
        return Task.FromResult<IHtmlContent>(new HtmlString(html));
    }
}
