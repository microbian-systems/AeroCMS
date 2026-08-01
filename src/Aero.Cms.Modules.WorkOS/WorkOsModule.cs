using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.WorkOS;

/// <summary>Registers the bounded WorkOS external-member provider integration.</summary>
[Module(nameof(WorkOsModule))]
public sealed class WorkOsModule : AeroModuleBase
{
    public override string Name => nameof(WorkOsModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => [];
    public override IReadOnlyList<string> Tags => [];

    public override void ConfigureServices(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddDataProtection();
        services.AddHttpClient<WorkOsAuthenticationClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.workos.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddScoped<IExternalMemberProviderStrategy, WorkOsExternalMemberProviderStrategy>();
        services.AddScoped<IManagerIdentityProviderStrategy, WorkOsManagerIdentityProviderStrategy>();
    }
}
