using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Repairs the initial administrator's CMS role membership after startup.
/// This makes early setup installations, created before CMS roles were provisioned,
/// usable without recreating their database.
/// </summary>
public sealed class InitialAdminRoleRepairService(
    IServiceScopeFactory scopeFactory,
    ILogger<InitialAdminRoleRepairService> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var setupState = await scope.ServiceProvider
            .GetRequiredService<ISetupStateStore>()
            .LoadAsync(cancellationToken);

        if (setupState?.IsComplete != true || string.IsNullOrWhiteSpace(setupState.AdminEmail))
        {
            return;
        }

        var result = await scope.ServiceProvider
            .GetRequiredService<ISetupIdentityBootstrapper>()
            .EnsureInitialAdminRoleAsync(setupState.AdminEmail, cancellationToken);

        if (!result.Succeeded)
        {
            logger.LogError(
                "Unable to repair CMS role membership for the setup administrator: {Errors}",
                string.Join("; ", result.Errors.Select(error => error.Description)));
            return;
        }

        logger.LogInformation("Verified CMS role membership for the setup administrator.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
