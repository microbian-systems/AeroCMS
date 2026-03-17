using Aero.Cms.Core;

namespace Aero.Cms.Banners;

public class BannerModule : AeroModuleBase
{
    public override string Name => nameof(BannerModule);

    public override string Version => "1.0.0";

    public override string Author => "Microbian Systems";

    public override IReadOnlyList<string> Dependencies => [];

    public override string Description => "Shows banners at the top of the Aero CMS homepage";
}