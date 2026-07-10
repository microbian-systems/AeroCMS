using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Auth.Services;
using Aero.Modular;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Represents a class for SecurityModule.
/// </summary>
[Module(nameof(SecurityModule))]
public class SecurityModule : AeroModuleBase, IConfigureAeroDB
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(SecurityModule);
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
public override IReadOnlyList<string> Category => [];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => [];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        if (config != null)
        {
            services.Configure<ApiKeyOptions>(config.GetSection("Aero:Security:ApiKeys"));
        }

        services.AddScoped<IApiKeyFactory, DefaultApiKeyFactory>();
        services.AddScoped<IApiKeyGenerator, HashedApiKeyGenerator>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthenticationStrategy, ApiKeyAuthenticationStrategy>();

        // JWT token services — required by HeadlessModule JWT API endpoints
        services.AddMemoryCache();
        services.AddSingleton<IJwtSigningKeyPersistence, InMemoryJwtSigningKeyPersistence>();
        services.AddScoped<IJwtSigningKeyStore, JwtSigningKeyStore>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public override void Configure(IAeroModuleBuilder builder)
    {
        // Admin UI registration
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(StoreOptions opts)
    {
        opts.Schema.For<ApiKeyDocument>().Index(x => x.SecretHash);
        opts.Schema.For<ApiKeyDocument>().Index(x => x.UserId);
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }
}
