using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.AiAssistant;

/// <summary>Registers the provider-backed manager assistant without owning provider credentials.</summary>
[Module(nameof(AiAssistantModule))]
public sealed class AiAssistantModule : AeroWebModule
{
    public override string Name => nameof(AiAssistantModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override string Description => "Bounded provider-backed conversations for the AeroCMS manager.";
    public override IReadOnlyList<string> Dependencies => ["AiModule"];
    public override IReadOnlyList<string> Category => ["admin", "ai"];
    public override IReadOnlyList<string> Tags => ["ai", "assistant", "manager"];

    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration? config = null,
        IHostEnvironment? env = null)
    {
        services.AddScoped<IAeroCmsAssistantService, AeroCmsAssistantService>();
    }
}
