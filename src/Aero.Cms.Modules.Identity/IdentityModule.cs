using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Core.Identity;
using Aero.Models.Entities;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Modular;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Represents a class for IdentityModule.
/// </summary>
[Module(nameof(IdentityModule))]
public class IdentityModule : AeroWebModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(IdentityModule);
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
public override IReadOnlyList<string> Category => ["Identity", "Security"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["auth", "identity", "users", "roles"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddIdentityCore<AeroUser>()
            .AddRoles<AeroRole>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddAeroDBStores<AeroUser, AeroRole, long>();
    }
}
