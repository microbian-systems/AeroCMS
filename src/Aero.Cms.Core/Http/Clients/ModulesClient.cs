namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IModulesHttpClient
{
    Task<Result<string, IReadOnlyList<ModuleSummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, ModuleDetail>> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Result<string, ModuleDetail>> InstallAsync(InstallModuleRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> UninstallAsync(string id, CancellationToken ct = default);
    Task<Result<string, ModuleDetail>> EnableAsync(string id, CancellationToken ct = default);
    Task<Result<string, ModuleDetail>> DisableAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for modules endpoints (stub implementation).
/// </summary>
public class ModulesHttpClient(HttpClient httpClient, ILogger<ModulesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IModulesHttpClient
{
    protected override string ResourceName => "modules";

    public Task<Result<string, IReadOnlyList<ModuleSummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<ModuleSummary>>(string.Empty, ct);
    }

    public Task<Result<string, ModuleDetail>> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return GetResultAsync<ModuleDetail>($"details/{Uri.EscapeDataString(id)}", ct);
    }

    public Task<Result<string, ModuleDetail>> InstallAsync(InstallModuleRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<InstallModuleRequest, ModuleDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, bool>> UninstallAsync(string id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id, ct);
    }

    public Task<Result<string, ModuleDetail>> EnableAsync(string id, CancellationToken ct = default)
    {
        return PostResultAsync<object, ModuleDetail>($"{id}/enable", new object(), ct);
    }

    public Task<Result<string, ModuleDetail>> DisableAsync(string id, CancellationToken ct = default)
    {
        return PostResultAsync<object, ModuleDetail>($"{id}/disable", new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record ModuleSummary(string Id, string Name, string Version, string Author, bool IsEnabled, bool IsInstalled);
public record ModuleDetail(string Id, string Name, string Version, string Author, string Description, bool IsEnabled, bool IsInstalled, DateTime InstalledAt);
public record InstallModuleRequest(string ModuleId, string Version);
