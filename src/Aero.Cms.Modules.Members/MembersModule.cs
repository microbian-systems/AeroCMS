using Aero.Modular;

namespace Aero.Cms.Modules.Members;

/// <summary>
/// Declares the module boundary for site members who are not CMS administration users.
/// </summary>
[Module(nameof(MembersModule))]
public class MembersModule : AeroModuleBase
{
    /// <inheritdoc />
public override string Name { get; }
    /// <inheritdoc />
public override string Version { get; }
    /// <inheritdoc />
public override string Author { get; }
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies { get; }
    /// <inheritdoc />
public override IReadOnlyList<string> Category { get; }
    /// <inheritdoc />
public override IReadOnlyList<string> Tags { get; }
}