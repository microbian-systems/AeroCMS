using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Aero.Cms.Modules.Jwt;

public class JwtAuthModule : ModuleBase
{
    public override string Name => "JWT Authentication";
    public override string Version => "1.0.0";
    public override string Author => "Aero.Cms";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public override void ConfigureServices(IServiceCollection services)
    {
        var key = Encoding.UTF8.GetBytes("super-secret-key");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

        services.AddAuthorization();
    }

    public override void Init(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/login", (LoginRequest req) =>
        {
            if (req.Email != "admin" || req.Password != "password")
                return Results.Unauthorized();

            // Note: JwtTokenGenerator is assumed to be defined elsewhere or handled in future tasks
            // var token = JwtTokenGenerator.Generate(req.Email);
            // return Results.Ok(new { token });
            return Results.Ok(new { token = "placeholder-token" });
        });
    }
}
