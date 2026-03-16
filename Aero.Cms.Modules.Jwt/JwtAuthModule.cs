using Aero.Cms.Core;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Jwt;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Reflection.Emit;
using System.Text;

public class JwtAuthModule : AeroModuleBase
{

    public override string Name => "";

    public override string Version => "";

    public override string Author => "";

    public override IReadOnlyList<string> Dependencies => [];

    public override string Description => "";

    public void ConfigureServices(IServiceCollection services)
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

    public void Init(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/login", (LoginRequest req) =>
        {
            if (req.Email != "admin" || req.Password != "password")
                return Results.Unauthorized();

            var token = JwtTokenGenerator.Generate(req.Email);

            return Results.Ok(new { token });
        });
    }

    public Task InitAsync(WebApplication app) => Task.CompletedTask;

    public void Configure(IModuleBuilder builder) { }
}