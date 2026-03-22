using System.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Core.Blocks.Rendering.Components;

/// <summary>
/// ViewComponent for rendering embed blocks for external content (videos, social posts, etc.).
/// </summary>
/// <remarks>
/// This component renders embedded content from an <see cref="EmbedBlock"/>.
/// Supports various embed types including YouTube, Vimeo, and generic iframe embeds.
/// </remarks>
public class EmbedBlockViewComponent : ViewComponent
{
    /// <summary>
    /// Invokes the view component asynchronously to render the embed block.
    /// </summary>
    /// <param name="block">The embed block to render. Must be of type <see cref="EmbedBlock"/>.</param>
    /// <returns>An <see cref="IHtmlContent"/> containing the rendered HTML embed element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the block is not an <see cref="EmbedBlock"/>.</exception>
    public Task<IHtmlContent> InvokeAsync(BlockBase block)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (block is not EmbedBlock embedBlock)
        {
            throw new InvalidOperationException($"Expected block of type {nameof(EmbedBlock)} but received {block.GetType().Name}");
        }

        var escapedUrl = HttpUtility.HtmlAttributeEncode(embedBlock.SourceUrl);
        var embedType = embedBlock.EmbedType.ToLowerInvariant();

        var html = embedType switch
        {
            "youtube" => RenderYouTubeEmbed(escapedUrl, embedBlock.ThumbnailUrl),
            "vimeo" => RenderVimeoEmbed(escapedUrl, embedBlock.ThumbnailUrl),
            _ => RenderGenericEmbed(escapedUrl, embedBlock.ThumbnailUrl)
        };

        return Task.FromResult<IHtmlContent>(new HtmlString(html));
    }

    /// <summary>
    /// Renders a YouTube embed with responsive wrapper.
    /// </summary>
    /// <param name="url">The YouTube video URL.</param>
    /// <param name="thumbnailUrl">Optional thumbnail URL.</param>
    /// <returns>The HTML string for the YouTube embed.</returns>
    private static string RenderYouTubeEmbed(string url, string? thumbnailUrl)
    {
        var embedUrl = ConvertToYouTubeEmbedUrl(url);

        return $"""
            <div class="embed my-6">
                <div class="relative w-full" style="padding-bottom: 56.25%;">
                    <iframe 
                        src="{embedUrl}" 
                        class="absolute top-0 left-0 w-full h-full rounded-lg shadow-md"
                        frameborder="0" 
                        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" 
                        allowfullscreen
                        loading="lazy">
                    </iframe>
                </div>
            </div>
            """;
    }

    /// <summary>
    /// Renders a Vimeo embed with responsive wrapper.
    /// </summary>
    /// <param name="url">The Vimeo video URL.</param>
    /// <param name="thumbnailUrl">Optional thumbnail URL.</param>
    /// <returns>The HTML string for the Vimeo embed.</returns>
    private static string RenderVimeoEmbed(string url, string? thumbnailUrl)
    {
        var embedUrl = ConvertToVimeoEmbedUrl(url);

        return $"""
            <div class="embed my-6">
                <div class="relative w-full" style="padding-bottom: 56.25%;">
                    <iframe 
                        src="{embedUrl}" 
                        class="absolute top-0 left-0 w-full h-full rounded-lg shadow-md"
                        frameborder="0" 
                        allow="autoplay; fullscreen; picture-in-picture" 
                        allowfullscreen
                        loading="lazy">
                    </iframe>
                </div>
            </div>
            """;
    }

    /// <summary>
    /// Renders a generic iframe embed with responsive wrapper.
    /// </summary>
    /// <param name="url">The source URL.</param>
    /// <param name="thumbnailUrl">Optional thumbnail URL.</param>
    /// <returns>The HTML string for the generic embed.</returns>
    private static string RenderGenericEmbed(string url, string? thumbnailUrl)
    {
        return $"""
            <div class="embed my-6">
                <div class="relative w-full" style="padding-bottom: 56.25%;">
                    <iframe 
                        src="{url}" 
                        class="absolute top-0 left-0 w-full h-full rounded-lg shadow-md"
                        frameborder="0" 
                        allowfullscreen
                        loading="lazy">
                    </iframe>
                </div>
            </div>
            """;
    }

    /// <summary>
    /// Converts a YouTube URL to an embed URL.
    /// </summary>
    /// <param name="url">The YouTube URL (watch, short, or embed).</param>
    /// <returns>The embed URL.</returns>
    private static string ConvertToYouTubeEmbedUrl(string url)
    {
        // Handle youtube.com/watch?v=VIDEO_ID
        if (url.Contains("youtube.com/watch"))
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var videoId = query["v"];
            if (!string.IsNullOrEmpty(videoId))
            {
                return $"https://www.youtube.com/embed/{videoId}";
            }
        }

        // Handle youtu.be/VIDEO_ID
        if (url.Contains("youtu.be/"))
        {
            var videoId = url.Split('/').Last().Split('?').First();
            return $"https://www.youtube.com/embed/{videoId}";
        }

        return url;
    }

    /// <summary>
    /// Converts a Vimeo URL to an embed URL.
    /// </summary>
    /// <param name="url">The Vimeo URL.</param>
    /// <returns>The embed URL.</returns>
    private static string ConvertToVimeoEmbedUrl(string url)
    {
        // Handle vimeo.com/VIDEO_ID
        if (url.Contains("vimeo.com/"))
        {
            var videoId = url.Split('/').Last().Split('?').First();
            return $"https://player.vimeo.com/video/{videoId}";
        }

        return url;
    }
}
