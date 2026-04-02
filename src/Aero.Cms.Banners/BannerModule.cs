using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.Banners;

public class BannerModule : AeroModuleBase
{
    public override string Name => nameof(BannerModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Content", "Marketing"];
    public override IReadOnlyList<string> Tags => ["banners", "ads", "promotions"];

}
