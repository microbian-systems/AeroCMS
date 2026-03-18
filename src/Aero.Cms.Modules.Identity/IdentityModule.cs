using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Aero.Models.Entities;
using Aero.Core.Identity;
using Aero.MartenDB.Identity;

namespace Aero.Cms.Modules.Identity;

public class IdentityModule : ModuleBase
{
    public override string Name => "Identity";
    public override string Version => "1.0.0";
    public override string Author => "Aero.Cms";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIdentityCore<AeroUser>()
            .AddRoles<AeroRole>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserStore<AeroUser>, UserStore<AeroUser, AeroRole>>();
        services.AddScoped<IRoleStore<AeroRole>, RoleStore<AeroRole>>();
    }

    public override void Init(IEndpointRouteBuilder endpoints)
    {
    }
}
