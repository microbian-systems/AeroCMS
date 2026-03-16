using Aero.Cms.Core;

namespace Aero.Cms.CookiePolicy;

public class CookiePolicyModule : AeroModuleBase
{
    public override string Name => nameof(CookiePolicyModule);

    public override string Version => "1.0.0";

    public override string Author => "Microbian Systems";

    public override IReadOnlyList<string> Dependencies => [];


    public override string Description => "Adds a cookie policy popup on a users first visit to the site (homepage)";
}