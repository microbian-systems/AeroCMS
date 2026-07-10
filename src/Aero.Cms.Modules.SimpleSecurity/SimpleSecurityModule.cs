using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.SimpleSecurity;

/// <summary>
/// Represents a class for SimpleSecurityModule.
/// </summary>
[Module(nameof(SimpleSecurityModule))]
public class SimpleSecurityModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(SimpleSecurityModule);
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
public override IReadOnlyList<string> Category => ["Security"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["security", "simple", "auth"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public override void Configure(IAeroModuleBuilder builder)
    {
    }
}
