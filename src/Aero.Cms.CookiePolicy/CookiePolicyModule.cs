using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.CookiePolicy;

/// <summary>
/// Represents a class for CookiePolicyModule.
/// </summary>
[Module(nameof(CookiePolicyModule))]
public class CookiePolicyModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(CookiePolicyModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["Privacy", "Standard"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["cookies", "gdpr", "policy"];
}
