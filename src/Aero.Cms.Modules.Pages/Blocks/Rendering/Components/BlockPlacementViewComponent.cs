using Aero.Cms.Modules.Pages.Models;
using Marten;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Aero.Cms.Modules.Pages.Blocks.Rendering.Components;

/// <summary>
/// ViewComponent that resolves a block by ID from BlockPlacement and dispatches to the correct block ViewComponent.
/// </summary>
public class BlockPlacementViewComponent : ViewComponent
{
    private readonly IDocumentSession _session;
    private readonly IViewComponentHelper _componentHelper;

    /// <summary>
    /// Mapping from BlockType string to the corresponding ViewComponent name.
    /// </summary>
    private static readonly Dictionary<string, string> BlockTypeToViewComponent = new()
    {
        ["rich_text"] = "RichTextBlock",
        ["heading"] = "HeadingBlock",
        ["image"] = "ImageBlock",
        ["cta"] = "CtaBlock",
        ["quote"] = "QuoteBlock",
        ["embed"] = "EmbedBlock"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockPlacementViewComponent"/> class.
    /// </summary>
    /// <param name="session">The Marten document session for loading blocks.</param>
    /// <param name="componentHelper">The view component helper for invoking child components.</param>
    public BlockPlacementViewComponent(IDocumentSession session, IViewComponentHelper componentHelper)
    {
        _session = session;
        _componentHelper = componentHelper;
    }

    /// <summary>
    /// Invokes the view component asynchronously to render a block placement.
    /// </summary>
    /// <param name="placement">The block placement containing the block ID and type.</param>
    /// <returns>An <see cref="IHtmlContent"/> containing the rendered block.</returns>
    public async Task<IHtmlContent> InvokeAsync(BlockPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        // Load the block from Marten by ID
        var block = await _session.LoadAsync<Core.Blocks.BlockBase>(placement.BlockId);

        if (block is null)
        {
            return new HtmlString($"""<div class="text-red-500">Block with ID {placement.BlockId} not found</div>""");
        }

        // Map BlockType to ViewComponent name and invoke it
        if (!BlockTypeToViewComponent.TryGetValue(placement.BlockType, out var viewComponentName))
        {
            return new HtmlString($"""<div class="text-red-500">Unknown block type: {placement.BlockType}</div>""");
        }

        return await _componentHelper.InvokeAsync(viewComponentName, block);
    }
}
