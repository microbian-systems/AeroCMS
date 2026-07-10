using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Forum;

/// <summary>
/// reddit style forum module for async discussions
/// </summary>
[Module(nameof(AeroForumModule))]
public class AeroForumModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name { get; } = nameof(AeroForumModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version { get; } = AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author { get; } = AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies { get; } = [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category { get; } = [];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags { get; } = [];
}