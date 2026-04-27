
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aero.Cms.Abstractions.Blocks;

namespace Aero.Cms.Shared.Blocks.Rendering;

public partial class NavigationBlockRenderer
{
    [Parameter]
    public NavigationBlock? Block { get; set; }

    [Parameter]
    public NavigationDetail? Navigation { get; set; }
}