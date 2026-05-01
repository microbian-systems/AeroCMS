using Aero.Cms.Web.Core.Modules;
using Aero.Modular;

namespace Aero.Cms.Modules.Members;

/// <summary>
/// Used to manage site membership (non cms users)
/// </summary>
public class MembersModule : AeroModuleBase
{
    public override string Name { get; }
    public override string Version { get; }
    public override string Author { get; }
    public override IReadOnlyList<string> Dependencies { get; }
    public override IReadOnlyList<string> Category { get; }
    public override IReadOnlyList<string> Tags { get; }
}