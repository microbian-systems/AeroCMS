using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Rewrite;

/// <summary>
/// Provides discovery metadata for the Aero CMS rewrite module.
/// </summary>
/// <remarks>
/// <para>
/// This project is a metadata-only module shell. It declares no rewrite rules, service
/// registrations, startup filter, endpoint mapping, or middleware-pipeline contribution.
/// Loading it therefore does not inspect or modify request paths, query strings, hosts, route
/// values, response status codes, or <c>Location</c> headers, and it cannot terminate or
/// short-circuit a request.
/// </para>
/// <para>
/// Alias redirects are implemented by the separate <c>Aero.Cms.Modules.Aliases</c> project.
/// This project neither references that project nor declares it as a module dependency. No
/// alias rule ordering, site or culture scoping, path normalization, query preservation,
/// persistence fallback, or cache behavior should be inferred from this type.
/// </para>
/// <para>
/// The class inherits the no-op configuration and run lifecycle from
/// <see cref="AeroModuleBase"/>. It holds no state, starts no background work, accepts no
/// cancellation token, owns no disposable resource, and introduces no module-specific failure
/// behavior.
/// </para>
/// <para>
/// Because this module processes no redirect target, it neither creates an open-redirect risk
/// nor provides an open-redirect defense. Redirect validation and other security boundaries
/// remain the responsibility of whichever module or host actually installs rewrite rules.
/// </para>
/// </remarks>
[Module(nameof(RewriteModule))]
public class RewriteModule : AeroModuleBase
{
    /// <summary>
    /// Gets the fixed module-discovery name.
    /// </summary>
    public override string Name => nameof(RewriteModule);

    /// <summary>
    /// Gets the Aero CMS version reported by this module.
    /// </summary>
    public override string Version => AeroConstants.Version;

    /// <summary>
    /// Gets the Aero CMS author metadata reported by this module.
    /// </summary>
    public override string Author => AeroConstants.Author;

    /// <summary>
    /// Gets the module names that must load before this module.
    /// </summary>
    /// <remarks>The rewrite metadata module declares no module dependency.</remarks>
    public override IReadOnlyList<string> Dependencies => [];

    /// <summary>
    /// Gets the module-discovery categories.
    /// </summary>
    public override IReadOnlyList<string> Category => ["Infrastructure", "Routing"];

    /// <summary>
    /// Gets the module-discovery tags.
    /// </summary>
    public override IReadOnlyList<string> Tags => ["rewrite", "redirect", "routing", "url"];
}
