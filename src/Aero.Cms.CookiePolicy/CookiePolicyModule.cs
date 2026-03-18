using Aero.Cms.Core.Modules;

namespace Aero.Cms.CookiePolicy;

public class CookiePolicyModule : ModuleBase
{
    public override string Name => "Cookie Policy";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
}
