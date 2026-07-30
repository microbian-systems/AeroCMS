using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.RateLimiting;

/// <summary>
/// Registers the shared ASP.NET Core rate-limiting infrastructure used by AeroCMS feature modules.
/// </summary>
/// <remarks>
/// Feature modules contribute named policies through <see cref="AeroRateLimitServiceCollectionExtensions"/>.
/// The host remains responsible for calling <c>UseRateLimiter</c> exactly once after authentication and before
/// authorization.
/// </remarks>
[Module(nameof(RateLimitingModule))]
public sealed class RateLimitingModule : AeroModuleBase
{
    /// <inheritdoc />
    public override string Name => nameof(RateLimitingModule);

    /// <inheritdoc />
    public override string Version => AeroConstants.Version;

    /// <inheritdoc />
    public override string Author => AeroConstants.Author;

    /// <inheritdoc />
    public override IReadOnlyList<string> Dependencies => [];

    /// <inheritdoc />
    public override IReadOnlyList<string> Category => ["Security", "Infrastructure"];

    /// <inheritdoc />
    public override IReadOnlyList<string> Tags => ["ratelimit", "security", "throttling"];

    /// <summary>
    /// Registers common rejection handling and infrastructure options.
    /// </summary>
    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration? config = null,
        IHostEnvironment? env = null)
    {
        var infrastructureOptions = AeroRateLimitConfiguration.ReadInfrastructureOptions(config);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
                AeroRateLimitRejectionWriter.WriteAsync(
                    context,
                    infrastructureOptions,
                    cancellationToken);
        });
    }
}
