using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Modules;
using Aero.Cms.Web.Core.Modules;
using Marten;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Auth.Services;
using Aero.Common.Web.Infrastructure;

namespace Aero.Cms.Modules.Security;

public class SecurityModule : AeroModuleBase
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
    }

    public override void Configure(IAeroModuleBuilder builder)
    {
        // Admin UI registration
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<ApiKeyDocument>().Index(x => x.SecretHash);
        opts.Schema.For<ApiKeyDocument>().Index(x => x.UserId);
    }
}
