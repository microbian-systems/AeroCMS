namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for modules HTTP client.
/// </summary>
public interface IModulesHttpClient
{
    /// <summary>
    /// Gets all available modules.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of module summaries or an error.</returns>
    Task<Result<IReadOnlyList<ModuleSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a module's details by its identifier.
    /// </summary>
    /// <param name="id">The module identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The module detail or an error.</returns>
    Task<Result<ModuleDetail, AeroError>> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Installs a new module.
    /// </summary>
    /// <param name="request">The install module request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The module detail or an error.</returns>
    Task<Result<ModuleDetail, AeroError>> InstallAsync(InstallModuleRequest request, CancellationToken ct = default);

    /// <summary>
    /// Uninstalls a module.
    /// </summary>
    /// <param name="id">The module identifier to uninstall.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if uninstallation was successful or an error.</returns>
    Task<Result<bool, AeroError>> UninstallAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Enables a module.
    /// </summary>
    /// <param name="id">The module identifier to enable.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated module detail or an error.</returns>
    Task<Result<ModuleDetail, AeroError>> EnableAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Disables a module.
    /// </summary>
    /// <param name="id">The module identifier to disable.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated module detail or an error.</returns>
    Task<Result<ModuleDetail, AeroError>> DisableAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for modules endpoints.
/// </summary>
public class ModulesHttpClient(HttpClient httpClient, ILogger<ModulesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IModulesHttpClient
{
    /// <inheritdoc />
    public override string Path => "modules";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ModuleSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<ModuleSummary>>(string.Empty, ct);
    }

    /// <inheritdoc />
    public Task<Result<ModuleDetail, AeroError>> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return GetAsync<ModuleDetail>($"details/{Uri.EscapeDataString(id)}", ct);
    }

    /// <inheritdoc />
    public Task<Result<ModuleDetail, AeroError>> InstallAsync(InstallModuleRequest request, CancellationToken ct = default)
    {
        return PostAsync<InstallModuleRequest, ModuleDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> UninstallAsync(string id, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync(id, ct));
    }

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var response = await task;
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from HTTP operation")
        };
    }

    /// <inheritdoc />
    public Task<Result<ModuleDetail, AeroError>> EnableAsync(string id, CancellationToken ct = default)
    {
        return PostAsync<object, ModuleDetail>($"{id}/enable", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<ModuleDetail, AeroError>> DisableAsync(string id, CancellationToken ct = default)
    {
        return PostAsync<object, ModuleDetail>($"{id}/disable", new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a module.
/// </summary>
public record ModuleSummary(string Id, string Name, string Version, string Author, bool IsEnabled, bool IsInstalled);

/// <summary>
/// Detailed module information.
/// </summary>
public record ModuleDetail(string Id, string Name, string Version, string Author, string Description, bool IsEnabled, bool IsInstalled, DateTime InstalledAt);

/// <summary>
/// Request to install a module.
/// </summary>
public record InstallModuleRequest(string ModuleId, string Version);
