using Aero.Cms.Abstractions.Http.Clients;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Modules.Areas.Api.v1;

/// <summary>
/// Maps administrative endpoints over the modules currently registered in dependency injection.
/// </summary>
/// <remarks>
/// The route group does not add an authorization requirement. Hosts must apply an authorization
/// convention or middleware policy before exposing module state. Enable and disable operations
/// mutate only the in-memory module instance and are not persisted.
/// </remarks>
public static class ModulesApi
{
    /// <summary>
    /// Maps the Modules Admin API endpoints.
    /// </summary>
    /// <param name="app">The endpoint route builder that receives the module routes.</param>
    public static void MapModulesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/modules")
            .WithTags("Admin - Modules");

        group.MapGet("/", GetAllModules)
            .WithName("GetAllModules");

        group.MapGet("/details/{id}", GetModuleById)
            .WithName("GetModuleById");

        group.MapPost("/{id}/enable", EnableModule)
            .WithName("EnableModule");

        group.MapPost("/{id}/disable", DisableModule)
            .WithName("DisableModule");

        group.MapPost("/", InstallModule)
            .WithName("InstallModule");

        group.MapDelete("/{id}", UninstallModule)
            .WithName("UninstallModule");
    }

    /// <summary>
    /// Lists registered modules as installed module summaries.
    /// </summary>
    /// <returns>HTTP 200 with summaries, or HTTP 500 exposing the caught exception message.</returns>
    /// <remarks>The cancellation token is accepted by binding but is not observed.</remarks>
    private static async Task<IResult> GetAllModules(
        [FromServices] IServiceProvider sp,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ModulesApi));
        try
        {
            var modules = sp.GetServices<IAeroModule>().ToList();

            var summaries = modules.Select(m => new ModuleSummary(
                m.Name,
                m.Name,
                m.Version,
                m.Author,
                !m.Disabled,
                true // If it's in the service provider, it's "installed" in this context
            )).ToList();

            return TypedResults.Ok(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all modules");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Finds a registered module by an ordinal case-sensitive name comparison.
    /// </summary>
    /// <returns>HTTP 200 with current runtime state, HTTP 404 when absent, or HTTP 500 on exceptions.</returns>
    /// <remarks>The reported installation time is the current UTC time, not a persisted timestamp.</remarks>
    private static async Task<IResult> GetModuleById(
        string id,
        [FromServices] IServiceProvider sp,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ModulesApi));
        try
        {
            var module = sp.GetServices<IAeroModule>().FirstOrDefault(m => m.Name == id);

            if (module is null)
            {
                return TypedResults.NotFound(new { error = $"Module with ID '{id}' not found." });
            }

            var detail = new ModuleDetail(
                module.Name,
                module.Name,
                module.Version,
                module.Author,
                module.Description ?? string.Empty,
                !module.Disabled,
                true,
                DateTime.UtcNow
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving module for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Enables a registered module instance for the lifetime of the current service graph.
    /// </summary>
    /// <returns>HTTP 200 after mutation, HTTP 404 when absent, or HTTP 500 on exceptions.</returns>
    /// <remarks>This operation does not persist state and does not re-run module startup behavior.</remarks>
    private static async Task<IResult> EnableModule(
        string id,
        [FromServices] IServiceProvider sp,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ModulesApi));
        try
        {
            var module = sp.GetServices<IAeroModule>().FirstOrDefault(m => m.Name == id);

            if (module is null)
            {
                return TypedResults.NotFound(new { error = $"Module with ID '{id}' not found." });
            }

            module.Disabled = false;
            // TODO: Persist state

            var detail = new ModuleDetail(
                module.Name,
                module.Name,
                module.Version,
                module.Author,
                module.Description ?? string.Empty,
                true,
                true,
                DateTime.UtcNow
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enabling module id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Disables a registered module instance for the lifetime of the current service graph.
    /// </summary>
    /// <returns>HTTP 200 after mutation, HTTP 404 when absent, or HTTP 500 on exceptions.</returns>
    /// <remarks>This operation does not persist state or remove services and endpoints already registered.</remarks>
    private static async Task<IResult> DisableModule(
        string id,
        [FromServices] IServiceProvider sp,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ModulesApi));
        try
        {
            var module = sp.GetServices<IAeroModule>().FirstOrDefault(m => m.Name == id);

            if (module is null)
            {
                return TypedResults.NotFound(new { error = $"Module with ID '{id}' not found." });
            }

            module.Disabled = true;
            // TODO: Persist state

            var detail = new ModuleDetail(
                module.Name,
                module.Name,
                module.Version,
                module.Author,
                module.Description ?? string.Empty,
                false,
                true,
                DateTime.UtcNow
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disabling module id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Reports that runtime module installation is not implemented.
    /// </summary>
    /// <returns>An HTTP 500 problem result.</returns>
    /// <remarks>The request body and cancellation token are currently unused.</remarks>
    private static async Task<IResult> InstallModule(
        [FromBody] InstallModuleRequest request,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ModulesApi));
        try
        {
            return TypedResults.Problem("Module installation via API not implemented.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error installing module");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Reports that runtime module uninstallation is not implemented.
    /// </summary>
    /// <returns>An HTTP 500 problem result.</returns>
    /// <remarks>The module identifier and cancellation token do not affect the response.</remarks>
    private static async Task<IResult> UninstallModule(
        string id,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ModulesApi));
        try
        {
            return TypedResults.Problem("Module uninstallation via API not implemented.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uninstalling module id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
