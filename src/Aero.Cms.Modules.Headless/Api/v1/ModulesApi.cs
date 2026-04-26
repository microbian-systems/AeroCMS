using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for module management.
/// </summary>
public static class ModulesApi
{
    /// <summary>
    /// Maps the Modules Admin API endpoints.
    /// </summary>
    public static void MapModulesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/modules")
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
