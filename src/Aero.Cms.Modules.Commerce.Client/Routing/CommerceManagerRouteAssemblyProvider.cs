using System.Reflection;
using Aero.Cms.Modules.Commerce.Client.Pages.Manager;
using Aero.Cms.Shared.Routing;

namespace Aero.Cms.Modules.Commerce.Client.Routing;

internal sealed class CommerceManagerRouteAssemblyProvider : IManagerRouteAssemblyProvider
{
    public Assembly Assembly => typeof(CommerceOverview).Assembly;
}
