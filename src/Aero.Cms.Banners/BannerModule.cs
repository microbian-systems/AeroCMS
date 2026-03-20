using Aero.Cms.Core.Modules;

namespace Aero.Cms.Banners;

public class BannerModule : AeroModuleBase
{
    public override string Name => nameof(BannerModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Content", "Marketing"];
    public override IReadOnlyList<string> Tags => ["banners", "ads", "promotions"];

}
