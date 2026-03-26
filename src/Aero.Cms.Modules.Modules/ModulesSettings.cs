using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.Modules.Modules;

internal class ModulesSettings : AeroModuleBase
{
    public override string Name => nameof(ModulesSettings);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Settings"];
    public override IReadOnlyList<string> Tags => ["modules", "settings", "configuration"];
}