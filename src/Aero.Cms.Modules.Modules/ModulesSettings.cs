using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.Modules.Modules;

internal class ModulesSettings : AeroModuleBase
{
    public override string Name => nameof(ModulesSettings);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Settings"];
    public override IReadOnlyList<string> Tags => ["modules", "settings", "configuration"];
}