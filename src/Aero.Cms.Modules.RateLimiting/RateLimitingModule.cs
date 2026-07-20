using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.RateLimiting;

/// <summary>
/// Registers the named <c>Global</c> ASP.NET Core fixed-window rate-limiter policy.
/// </summary>
/// <remarks>
/// The policy allows up to 100 permits per one-second window and queues no excess requests. This project does not
/// add rate-limiter middleware or apply the policy to any endpoint, so registration alone does not throttle traffic.
/// It configures no tenant, user, or client-IP partition, proxy trust rule, distributed state, rejection callback,
/// response status, or retry metadata. Consequently, this module does not establish global protection across
/// application instances or a trusted-client identity boundary.
/// </remarks>
[Module(nameof(RateLimitingModule))]
public class RateLimitingModule : AeroModuleBase
{
    /// <summary>Gets the fixed name used to discover this module.</summary>
    public override string Name => nameof(RateLimitingModule);

    /// <summary>Gets the Aero CMS version reported by this module.</summary>
    public override string Version => AeroConstants.Version;

    /// <summary>Gets the Aero CMS author metadata reported by this module.</summary>
    public override string Author => AeroConstants.Author;

    /// <summary>Gets an empty module dependency list.</summary>
    public override IReadOnlyList<string> Dependencies => [];

    /// <summary>Gets the security and infrastructure discovery categories.</summary>
    public override IReadOnlyList<string> Category => ["Security", "Infrastructure"];

    /// <summary>Gets descriptive rate-limiting discovery tags.</summary>
    public override IReadOnlyList<string> Tags => ["ratelimit", "security", "throttling"];

    /// <summary>
    /// Adds the named <c>Global</c> policy to ASP.NET Core rate-limiting services.
    /// </summary>
    /// <param name="services">The service collection that receives rate-limiter options.</param>
    /// <param name="config">Unused; limits and algorithm are fixed in code.</param>
    /// <param name="env">Unused; registration is not environment-gated.</param>
    /// <remarks>
    /// Registration is synchronous and exposes no cancellation token. Configuration failures are not caught. The
    /// method does not select queue processing order because <c>QueueLimit</c> is zero.
    /// </remarks>
    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // todo - enable database config to supply the type of rate limiting (sliding window, fixed, etc)
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("Global", opt =>
            {
                opt.Window = TimeSpan.FromSeconds(1);
                opt.PermitLimit = 100;
                opt.QueueLimit = 0;
            });
        });
    }
}
