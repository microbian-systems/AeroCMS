using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Auth.Services;
using Aero.Modular;
using AeroDB;

namespace Aero.Cms.Modules.Security;

[Module(nameof(SecurityModule))]
public class SecurityModule : AeroModuleBase, IConfigureAeroDB
{
    public override string Name => nameof(SecurityModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => [];
    public override IReadOnlyList<string> Tags => [];

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

    public override void Configure(IAeroModuleBuilder builder)
    {
        // Admin UI registration
    }

    public void Configure(StoreOptions opts)
    {
        opts.Schema.For<ApiKeyDocument>().Index(x => x.SecretHash);
        opts.Schema.For<ApiKeyDocument>().Index(x => x.UserId);
    }

    public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }
}
