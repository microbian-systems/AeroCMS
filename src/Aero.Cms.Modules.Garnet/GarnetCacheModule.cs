using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.Modules.Garnet;

public class GarnetCacheModule : AeroModuleBase
{
    public override string Name => nameof(GarnetCacheModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Performance"];
    public override IReadOnlyList<string> Tags => ["cache", "garnet", "microsoft", "performance"];
}
