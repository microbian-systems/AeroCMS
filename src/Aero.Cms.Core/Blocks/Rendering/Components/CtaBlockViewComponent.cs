using System.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Core.Blocks.Rendering.Components;

/// <summary>
/// ViewComponent for rendering call-to-action button blocks.
/// </summary>
/// <remarks>
/// This component renders a CTA button from a <see cref="CtaBlock"/> as a styled anchor element.
/// Supports different style variants (primary, secondary, outline) via the Style property.
/// </remarks>
public class CtaBlockViewComponent : ViewComponent
{
    /// <summary>
    /// Invokes the view component asynchronously to render the CTA block.
    /// </summary>
    /// <param name="block">The CTA block to render. Must be of type <see cref="CtaBlock"/>.</param>
    /// <returns>An <see cref="IHtmlContent"/> containing the rendered HTML anchor element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the block is not a <see cref="CtaBlock"/>.</exception>
    public Task<IHtmlContent> InvokeAsync(BlockBase block)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (block is not CtaBlock ctaBlock)
        {
            throw new InvalidOperationException($"Expected block of type {nameof(CtaBlock)} but received {block.GetType().Name}");
        }

        var escapedText = HttpUtility.HtmlEncode(ctaBlock.Text);
        var escapedUrl = HttpUtility.HtmlAttributeEncode(ctaBlock.Url);
        var styleClass = GetStyleClass(ctaBlock.Style);

        var html = $"""<a href="{escapedUrl}" class="{styleClass}">{escapedText}</a>""";
        return Task.FromResult<IHtmlContent>(new HtmlString(html));
    }

    /// <summary>
    /// Gets the Tailwind CSS classes based on the CTA style.
    /// </summary>
    /// <param name="style">The style identifier (primary, secondary, outline).</param>
    /// <returns>The CSS class string for the specified style.</returns>
    private static string GetStyleClass(string? style)
    {
        const string baseClasses = "cta-button inline-block px-6 py-3 font-semibold rounded-lg transition-colors duration-200 ";

        return style?.ToLowerInvariant() switch
        {
            "secondary" => baseClasses + "bg-gray-600 text-white hover:bg-gray-700 focus:ring-2 focus:ring-gray-500 focus:ring-offset-2",
            "outline" => baseClasses + "border-2 border-blue-600 text-blue-600 hover:bg-blue-50 focus:ring-2 focus:ring-blue-500 focus:ring-offset-2",
            "primary" or _ => baseClasses + "bg-blue-600 text-white hover:bg-blue-700 focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
        };
    }
}
