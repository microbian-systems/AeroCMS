
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Rendering;

namespace Aero.Cms.Shared.Blocks.Rendering;

[CmsBlockRenderer(typeof(NavigationBlock))]
public partial class NavigationBlockRenderer
{
    [Parameter]
    public NavigationBlock? Block { get; set; }

    [Parameter]
    public NavigationDetail? Navigation { get; set; }
}
