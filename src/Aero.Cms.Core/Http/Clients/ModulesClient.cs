namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

/// <summary>
/// Typed client for modules endpoints (stub implementation).
/// </summary>
public class ModulesClient(HttpClient httpClient, ILogger<ModulesClient> logger) : AeroClientBase(httpClient, logger)
{
    protected override string ResourceName => "modules";

    public Task<IReadOnlyList<ModuleSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<ModuleSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<ModuleSummary>>(Array.Empty<ModuleSummary>());
    }

    public Task<ModuleDetail?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return GetAsync<ModuleDetail>($"details/{Uri.EscapeDataString(id)}", ct);
    }

    public Task<ModuleDetail?> InstallAsync(InstallModuleRequest request, CancellationToken ct = default)
    {
        return PostAsync<ModuleDetail?, InstallModuleRequest>(string.Empty, request, ct);
    }

    public Task<bool> UninstallAsync(string id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }

    public Task<ModuleDetail?> EnableAsync(string id, CancellationToken ct = default)
    {
        return PostAsync<ModuleDetail?, object>($"{id}/enable", new object(), ct) 
            ?? Task.FromResult<ModuleDetail?>(null);
    }

    public Task<ModuleDetail?> DisableAsync(string id, CancellationToken ct = default)
    {
        return PostAsync<ModuleDetail?, object>($"{id}/disable", new object(), ct) 
            ?? Task.FromResult<ModuleDetail?>(null);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record ModuleSummary(string Id, string Name, string Version, string Author, bool IsEnabled, bool IsInstalled);
public record ModuleDetail(string Id, string Name, string Version, string Author, string Description, bool IsEnabled, bool IsInstalled, DateTime InstalledAt);
public record InstallModuleRequest(string ModuleId, string Version);
