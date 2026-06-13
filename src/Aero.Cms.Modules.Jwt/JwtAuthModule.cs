using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Aero.Cms.Core;
using Aero.Cms.Modules.Jwt.Areas.Api.v1;
using Aero.Modular;

namespace Aero.Cms.Modules.Jwt;

[Module(nameof(JwtAuthModule))]
public class JwtAuthModule : AeroWebModule
{
    public override string Name => nameof(JwtAuthModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Identity", "Security"];
    public override IReadOnlyList<string> Tags => ["auth", "jwt", "tokens", "security"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        var key = Encoding.UTF8.GetBytes("super-secret-key");

        services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {
        // Map the /auth/login placeholder (pre-existing)
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/login", (LoginRequest req) =>
        {
            if (req.Email != "admin" || req.Password != "password")
                return Results.Unauthorized();

            return Results.Ok(new { token = "placeholder-token" });
        });

        // Then chain to RunAsync for the official Headless Auth/Jwt APIs
        base.Run(endpoints);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapJwtApi();
        builder.MapAuthApi();

        return Task.CompletedTask;
    }
}
