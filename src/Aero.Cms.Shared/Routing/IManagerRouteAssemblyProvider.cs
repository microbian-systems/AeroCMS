using System.Reflection;

namespace Aero.Cms.Shared.Routing;

/// <summary>Explicitly supplies a routable manager component assembly to the shared router.</summary>
public interface IManagerRouteAssemblyProvider
{
    /// <summary>Gets the assembly containing routable manager components.</summary>
    Assembly Assembly { get; }
}
