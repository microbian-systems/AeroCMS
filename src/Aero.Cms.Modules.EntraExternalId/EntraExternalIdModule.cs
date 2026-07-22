using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.EntraExternalId;

/// <summary>Registers the bounded Entra External ID external-member provider integration.</summary>
[Module(nameof(EntraExternalIdModule))]
public sealed class EntraExternalIdModule : AeroModuleBase
{
    public override string Name => nameof(EntraExternalIdModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["identity"];
    public override IReadOnlyList<string> Tags => ["external-members", "entra-external-id", "oidc"];

    public override void ConfigureServices(IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddDataProtection();
        services.AddHttpClient<EntraExternalIdHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddHttpClient<EntraWorkforceHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IEntraOpenIdConfigurationSource, EntraOpenIdConfigurationSource>();
        services.AddSingleton<IEntraWorkforceOpenIdConfigurationSource, EntraWorkforceOpenIdConfigurationSource>();
        services.AddScoped<IExternalMemberProviderStrategy, EntraExternalIdProviderStrategy>();
        services.AddScoped<IManagerIdentityProviderStrategy, EntraWorkforceManagerIdentityProviderStrategy>();
    }
}
