using System.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Core.Blocks.Rendering.Components;

/// <summary>
/// ViewComponent for rendering quote/blockquote blocks with optional author and citation.
/// </summary>
/// <remarks>
/// This component renders a quote from a <see cref="QuoteBlock"/> within a semantic blockquote element.
/// Optionally displays the author and citation information.
/// </remarks>
public class QuoteBlockViewComponent : ViewComponent
{
    /// <summary>
    /// Invokes the view component asynchronously to render the quote block.
    /// </summary>
    /// <param name="block">The quote block to render. Must be of type <see cref="QuoteBlock"/>.</param>
    /// <returns>An <see cref="IHtmlContent"/> containing the rendered HTML blockquote element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the block is not a <see cref="QuoteBlock"/>.</exception>
    public Task<IHtmlContent> InvokeAsync(BlockBase block)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (block is not QuoteBlock quoteBlock)
        {
            throw new InvalidOperationException($"Expected block of type {nameof(QuoteBlock)} but received {block.GetType().Name}");
        }

        var escapedContent = HttpUtility.HtmlEncode(quoteBlock.Content);

        var html = new System.Text.StringBuilder();
        html.Append("""<blockquote class="border-l-4 border-blue-500 pl-6 py-2 my-6 bg-gray-50 rounded-r-lg">""");
        html.Append($"""<p class="text-lg text-gray-800 italic mb-3">&ldquo;{escapedContent}&rdquo;</p>""");

        if (!string.IsNullOrWhiteSpace(quoteBlock.Author) || !string.IsNullOrWhiteSpace(quoteBlock.Citation))
        {
            html.Append("""<cite class="text-sm text-gray-600 not-italic">""");

            if (!string.IsNullOrWhiteSpace(quoteBlock.Author))
            {
                html.Append($"""<span class="font-semibold">{HttpUtility.HtmlEncode(quoteBlock.Author)}</span>""");
            }

            if (!string.IsNullOrWhiteSpace(quoteBlock.Citation))
            {
                if (!string.IsNullOrWhiteSpace(quoteBlock.Author))
                {
                    html.Append(""", <span class="text-gray-500">{HttpUtility.HtmlEncode(quoteBlock.Citation)}</span>""");
                }
                else
                {
                    html.Append($"""<span class="text-gray-500">{HttpUtility.HtmlEncode(quoteBlock.Citation)}</span>""");
                }
            }

            html.Append("""</cite>""");
        }

        html.Append("""</blockquote>""");

        return Task.FromResult<IHtmlContent>(new HtmlString(html.ToString()));
    }
}
