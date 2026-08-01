using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.CookiePolicy;

/// <summary>
/// Declares the Cookie Policy feature in Aero CMS module discovery.
/// </summary>
/// <remarks>
/// The current module exposes metadata only. It does not register consent services, cookie categories, middleware,
/// endpoints, UI components, persistence, or response-cookie behavior. The module name and its privacy-related
/// discovery tags therefore do not by themselves provide browser enforcement or legal-compliance guarantees.
/// </remarks>
[Module(nameof(CookiePolicyModule))]
public class CookiePolicyModule : AeroModuleBase
{
    /// <summary>Gets the fixed name used to discover this module.</summary>
    public override string Name => nameof(CookiePolicyModule);

    /// <summary>Gets the Aero CMS version reported by this module.</summary>
    public override string Version => AeroConstants.Version;

    /// <summary>Gets the Aero CMS author metadata reported by this module.</summary>
    public override string Author => AeroConstants.Author;

    /// <summary>Gets an empty dependency list; module metadata imposes no ordering dependency.</summary>
    public override IReadOnlyList<string> Dependencies => [];

    /// <summary>Gets the module-discovery categories <c>Privacy</c> and <c>Standard</c>.</summary>
    public override IReadOnlyList<string> Category => ["Privacy", "Standard"];

    /// <summary>Gets descriptive discovery tags; they do not configure a cookie policy.</summary>
    public override IReadOnlyList<string> Tags => ["cookies", "gdpr", "policy"];
}
