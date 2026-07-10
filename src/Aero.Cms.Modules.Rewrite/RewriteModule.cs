using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Rewrite;

/// <summary>
/// Represents a class for RewriteModule.
/// </summary>
[Module(nameof(RewriteModule))]
public class RewriteModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(RewriteModule);
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
public override IReadOnlyList<string> Category => ["Infrastructure", "Routing"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["rewrite", "redirect", "routing", "url"];



}
