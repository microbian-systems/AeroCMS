using Aero.AppServer;
using Aero.AppServer.Startup;

namespace Aero.Cms.Modules.Setup.Bootstrap;

internal static class BootstrapDatabaseScopeGuard
{
    internal static string? GetValidationError(
        SeedDatabaseRequest request,
        ResolvedInfrastructureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);

        if (!SurrealDatabaseScope.TryNormalize(request.DatabaseNamespace, out var databaseNamespace) ||
            !SurrealDatabaseScope.TryNormalize(request.DatabaseName, out var databaseName))
        {
            return "The pending setup payload contains an invalid SurrealDB namespace or database name.";
        }

        if (string.Equals(databaseNamespace, settings.DatabaseNamespace, StringComparison.Ordinal) &&
            string.Equals(databaseName, settings.DatabaseName, StringComparison.Ordinal))
        {
            return null;
        }

        return $"The pending setup database target '{databaseNamespace}/{databaseName}' does not match " +
               $"the configured runtime target '{settings.DatabaseNamespace}/{settings.DatabaseName}'.";
    }
}
