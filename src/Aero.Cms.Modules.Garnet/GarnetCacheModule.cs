using Aero.Cms.Core.Modules;

namespace Aero.Cms.Modules.Garnet;

public class GarnetCacheModule : AeroModuleBase
{
    public override string Name => "Garnet Cache";
    public override string Version => "1.0.0";
    public override string Author => "Aero.Cms";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
}
