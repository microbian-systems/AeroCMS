using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Forum;

/// <summary>
/// Identifies the forum module for asynchronous, threaded discussions.
/// </summary>
[Module(nameof(AeroForumModule))]
public class AeroForumModule : AeroModuleBase
{
    /// <inheritdoc />
public override string Name { get; } = nameof(AeroForumModule);
    /// <inheritdoc />
public override string Version { get; } = AeroConstants.Version;
    /// <inheritdoc />
public override string Author { get; } = AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies { get; } = [];
    /// <inheritdoc />
public override IReadOnlyList<string> Category { get; } = [];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags { get; } = [];
}