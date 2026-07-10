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

/// <summary>
/// Represents a class for JwtAuthModule.
/// </summary>
[Module(nameof(JwtAuthModule))]
public class JwtAuthModule : AeroWebModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(JwtAuthModule);
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
public override IReadOnlyList<string> Tags => ["auth", "jwt", "tokens", "security"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
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

        /// <summary>
    /// Run method.
    /// </summary>
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

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapJwtApi();
        builder.MapAuthApi();

        return Task.CompletedTask;
    }
}
