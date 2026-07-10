
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Rendering;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Represents a class for NavigationBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(NavigationBlock))]
public partial class NavigationBlockRenderer
{
        /// <summary>
    /// Gets or sets the Block.
    /// </summary>
[Parameter]
    public NavigationBlock? Block { get; set; }

        /// <summary>
    /// Gets or sets the Navigation.
    /// </summary>
[Parameter]
    public NavigationDetail? Navigation { get; set; }
}
