using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AeroDB.Sable;

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
            .EnsureRecoveryAdministratorAsync(
                setupState.RecoveryAdministratorUserId,
                setupState.AdminEmail,
                cancellationToken);

        if (!result.Succeeded)
        {
            logger.LogError(
                "Unable to repair CMS role membership for the setup administrator: {Errors}",
                string.Join("; ", result.Errors.Select(error => error.Description)));
            return;
        }

        if (setupState.RecoveryAdministratorUserId is null && result.AdminUser is not null)
        {
            setupState.RecoveryAdministratorUserId = result.AdminUser.Id;
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            session.Store(setupState);
            await session.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Verified the manager recovery administrator invariant.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
