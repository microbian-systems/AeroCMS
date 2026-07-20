using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Auth.Services;
using Aero.Models.Entities;
using Aero.Modular;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Registers API-key authentication helpers, JWT/refresh-token services, and related AeroDB indexes.
/// </summary>
/// <remarks>
/// This module registers services only. It does not add authentication or authorization middleware, configure an
/// ASP.NET Core authentication scheme, map endpoints, emit security headers, or apply tenant isolation. JWT signing
/// keys use the registered process-local in-memory persistence implementation. Signing and validation keys are lost
/// at restart and are not shared by multiple instances, so previously issued access tokens may become unverifiable
/// after restart or on another instance. Registration does not schedule key rotation or establish a signing-key
/// lifetime. Hosting code remains responsible for enforcement, durable key persistence, and secret-management
/// boundaries.
/// </remarks>
[Module(nameof(SecurityModule))]
public class SecurityModule : AeroModuleBase, IConfigureAeroDB
{
        /// <summary>
    /// Gets the fixed name used to discover this module.
    /// </summary>
public override string Name => nameof(SecurityModule);
        /// <summary>
    /// Gets the Aero CMS version reported by this module.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets the Aero CMS author metadata reported by this module.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets an empty module dependency list.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets an empty discovery-category list.
    /// </summary>
public override IReadOnlyList<string> Category => [];
        /// <summary>
    /// Gets an empty discovery-tag list.
    /// </summary>
public override IReadOnlyList<string> Tags => [];

        /// <summary>
    /// Registers scoped API-key/authentication/token services, memory cache, and in-memory JWT signing-key persistence.
    /// </summary>
    /// <remarks>
    /// When configuration is present, <c>Aero:Security:ApiKeys</c> is bound to <c>ApiKeyOptions</c>. The host
    /// environment is not used to alter registration. <c>JwtTokenService</c> reads the access-token lifetime from
    /// <c>Auth:AccessTokenLifetimeSeconds</c>, defaulting to 300 seconds; that token lifetime is not a signing-key
    /// lifetime or rotation interval, and this module does not validate or cap the configured value.
    /// <c>RefreshTokenService</c> independently reads <c>Auth:RefreshTokenLifetimeDays</c>, defaulting to 30 days, and
    /// persists hashed <c>RefreshToken</c> documents. It does not consume the plaintext
    /// <c>ApiAccountModel.RefreshToken</c> field created by <see cref="ApiKeyService.CreateKeyAsync"/>. Registration is
    /// synchronous and exceptions propagate.
    /// </remarks>
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
    /// Performs no module-builder or admin-UI registration.
    /// </summary>
public override void Configure(IAeroModuleBuilder builder)
    {
        // Admin UI registration
    }

        /// <summary>
    /// Adds non-unique AeroDB indexes for the digest-bearing <c>ApiAccountModel.ApiKey</c> property and for API-key
    /// document hashes and user identifiers.
    /// </summary>
    /// <remarks>
    /// These indexes improve lookup but do not prevent duplicate API-key digests. Validation returns the first enabled
    /// matching API-account document when duplicates exist.
    /// </remarks>
    public void Configure(StoreOptions opts)
    {
        // todo - Goes through the runtime reflection fallback in SchemaManager - move ApiAccountModel so we can inherit from ISableDocument<TId>
        opts.Schema.For<ApiAccountModel>()
            .Index(x => x.ApiKey);
        opts.Schema.For<ApiKeyDocument>()
            .Index(x => x.SecretHash);
        opts.Schema.For<ApiKeyDocument>()
            .Index(x => x.UserId);
    }

        /// <summary>
    /// Delegates service-provider-aware AeroDB configuration to <see cref="Configure(StoreOptions)"/>.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }
}
