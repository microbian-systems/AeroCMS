using Aero.Cms.Core;
using Aero.Cms.Marten.Identity;
using Aero.Cms.Web.Core.Modules;
using Aero.Core.Identity;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Identity;

public class IdentityModule : AeroWebModule
{
    public override string Name => nameof(IdentityModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Identity", "Security"];
    public override IReadOnlyList<string> Tags => ["auth", "identity", "users", "roles"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddIdentityCore<AeroUser>()
            .AddRoles<AeroRole>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddMartenStores();
    }
}
