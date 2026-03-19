using Aero.Cms.Core.Modules;

namespace Aero.Cms.Banners;

public class BannerModule : AeroModuleBase
{
    public override string Name => "Banners";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
}
