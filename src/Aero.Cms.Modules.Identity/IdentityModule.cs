using Aero.Cms.Core.Modules;
using Aero.Core.Identity;
using Aero.MartenDB.Identity;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Identity;

public class IdentityModule : AeroModuleBase
{
    public override string Name => nameof(IdentityModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Identity", "Security"];
    public override IReadOnlyList<string> Tags => ["auth", "identity", "users", "roles"];

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIdentityCore<AeroUser>()
            .AddRoles<AeroRole>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserStore<AeroUser>, UserStore<AeroUser, AeroRole>>();
        services.AddScoped<IRoleStore<AeroRole>, RoleStore<AeroRole>>();
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {
    }
}
