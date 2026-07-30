using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Core;
using Aero.Cms.Modules.Jwt.Areas.Api.v1;
using Aero.Modular;

namespace Aero.Cms.Modules.Jwt;

/// <summary>
/// Registers a JWT bearer-validation scheme and maps the module's login,
/// access-token, and refresh-token endpoints.
/// </summary>
/// <remarks>
/// Bearer validation resolves the same <c>IJwtSigningKeyStore</c> used by
/// <c>IJwtTokenService</c>, so token issuance and validation share the current
/// signing-key material.
/// The bearer scheme is registered without being explicitly selected as the
/// host's default authentication scheme. This module also does not add
/// authorization policies, token revocation, tenant scope, signing-key rotation,
/// or persistent key storage.
/// </remarks>
[Module(nameof(JwtAuthModule))]
public class JwtAuthModule : AeroWebModule
{
    /// <summary>
    /// The stable module identifier, <c>JwtAuthModule</c>.
    /// </summary>
    public override string Name => nameof(JwtAuthModule);
        /// <summary>
    /// The Aero CMS version reported in module metadata.
    /// </summary>
    public override string Version => AeroConstants.Version;
        /// <summary>
    /// The Aero CMS author reported in module metadata.
    /// </summary>
    public override string Author => AeroConstants.Author;
        /// <summary>
    /// An empty collection because the module declares no module-ordering dependencies.
    /// </summary>
    public override IReadOnlyList<string> Dependencies => ["SecurityModule"];
        /// <summary>
    /// The identity and security categories used to classify this module.
    /// </summary>
    public override IReadOnlyList<string> Category => ["Identity", "Security"];
        /// <summary>
    /// The authentication, JWT, token, and security discovery tags assigned to this module.
    /// </summary>
    public override IReadOnlyList<string> Tags => ["auth", "jwt", "tokens", "security"];

    /// <inheritdoc />
    /// <remarks>
    /// Registers Aero's JWT handler without explicitly making it the host's
    /// default authentication scheme. The handler validates token lifetime and
    /// the signature against all currently valid keys returned by the shared
    /// signing-key store. Issuer and audience validation remain disabled for the
    /// current headless-token contract. The configuration and environment
    /// parameters are ignored, and this method adds no authorization policies or
    /// middleware.
    /// </remarks>
    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AeroJwtAuthenticationHandler>(
                AeroJwtAuthenticationHandler.SchemeName,
                _ => { });
    }

    /// <summary>
    /// Maps a placeholder login endpoint and then maps the headless endpoints
    /// through <see cref="RunAsync(IEndpointRouteBuilder)"/>.
    /// </summary>
    /// <param name="endpoints">The route builder to update.</param>
    /// <remarks>
    /// <c>POST /auth/login</c> compares the supplied email and password with the
    /// literal values <c>admin</c> and <c>password</c>. A match returns the
    /// literal value <c>placeholder-token</c>; it does not issue a JWT, create a
    /// session, hash a credential, or load a user. The handler returns 200 for
    /// that exact pair and 401 otherwise. This JSON POST endpoint has no explicit
    /// authorization, antiforgery, or rate-limiting metadata. Host-level fallback
    /// policies and middleware can still affect access.
    /// </remarks>
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
    /// Maps the versioned headless authentication, token, and refresh endpoints.
    /// </summary>
    /// <param name="builder">The route builder to update.</param>
    /// <returns>A task that is already complete after endpoint registration.</returns>
    /// <remarks>
    /// Endpoint execution depends on Identity and token services registered by
    /// the host; this module declares no corresponding module dependency. The
    /// method performs no asynchronous work and accepts no startup cancellation
    /// token. The mapped JSON POST endpoints attach no antiforgery metadata.
    /// </remarks>
    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapJwtApi();
        builder.MapAuthApi();

        return Task.CompletedTask;
    }
}
