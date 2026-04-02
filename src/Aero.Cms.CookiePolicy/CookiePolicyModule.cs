using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.CookiePolicy;

public class CookiePolicyModule : AeroModuleBase
{
    public override string Name => nameof(CookiePolicyModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Privacy", "Standard"];
    public override IReadOnlyList<string> Tags => ["cookies", "gdpr", "policy"];
}
