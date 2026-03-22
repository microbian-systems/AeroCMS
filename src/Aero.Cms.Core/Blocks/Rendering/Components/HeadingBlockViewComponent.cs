using System.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Core.Blocks.Rendering.Components;

/// <summary>
/// ViewComponent for rendering heading blocks with configurable levels (H1-H6).
/// </summary>
/// <remarks>
/// This component renders heading text from a <see cref="HeadingBlock"/> with the appropriate
/// heading level (H1-H6) based on the block's Level property.
/// </remarks>
public class HeadingBlockViewComponent : ViewComponent
{
    /// <summary>
    /// Invokes the view component asynchronously to render the heading block.
    /// </summary>
    /// <param name="block">The heading block to render. Must be of type <see cref="HeadingBlock"/>.</param>
    /// <returns>An <see cref="IHtmlContent"/> containing the rendered HTML heading element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the block is not a <see cref="HeadingBlock"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the heading level is not between 1 and 6.</exception>
    public Task<IHtmlContent> InvokeAsync(BlockBase block)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (block is not HeadingBlock headingBlock)
        {
            throw new InvalidOperationException($"Expected block of type {nameof(HeadingBlock)} but received {block.GetType().Name}");
        }

        var level = Math.Clamp(headingBlock.Level, 1, 6);
        var escapedText = HttpUtility.HtmlEncode(headingBlock.Text);

        var html = level switch
        {
            1 => $"""<h1 class="text-4xl font-bold text-gray-900 mb-4">{escapedText}</h1>""",
            2 => $"""<h2 class="text-3xl font-bold text-gray-900 mb-3">{escapedText}</h2>""",
            3 => $"""<h3 class="text-2xl font-bold text-gray-900 mb-3">{escapedText}</h3>""",
            4 => $"""<h4 class="text-xl font-bold text-gray-900 mb-2">{escapedText}</h4>""",
            5 => $"""<h5 class="text-lg font-bold text-gray-900 mb-2">{escapedText}</h5>""",
            6 => $"""<h6 class="text-base font-bold text-gray-900 mb-2">{escapedText}</h6>""",
            _ => throw new ArgumentOutOfRangeException(nameof(level), $"Invalid heading level: {level}")
        };

        return Task.FromResult<IHtmlContent>(new HtmlString(html));
    }
}
