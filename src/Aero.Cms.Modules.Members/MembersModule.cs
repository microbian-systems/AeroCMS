using Aero.Modular;

namespace Aero.Cms.Modules.Members;

/// <summary>
/// Used to manage site membership (non cms users)
/// </summary>
[Module(nameof(MembersModule))]
public class MembersModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name { get; }
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version { get; }
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author { get; }
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies { get; }
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category { get; }
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags { get; }
}