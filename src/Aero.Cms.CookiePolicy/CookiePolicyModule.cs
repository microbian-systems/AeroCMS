using Aero.Cms.Core.Modules;

namespace Aero.Cms.CookiePolicy;

public class CookiePolicyModule : AeroModuleBase
{
    public override string Name => nameof(CookiePolicyModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Privacy", "Standard"];
    public override IReadOnlyList<string> Tags => ["cookies", "gdpr", "policy"];
}
