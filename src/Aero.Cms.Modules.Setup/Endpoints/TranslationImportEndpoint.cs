using Aero.Cms.Modules.Setup.Services;
using Aero.Core;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Endpoints;

/// <summary>
/// Maps the administrative endpoint that imports localization resources.
/// </summary>
public static class TranslationImportEndpoint
{
    /// <summary>
    /// Maps the translation import endpoint beneath the administrative localization route.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to modify.</param>
    /// <returns>The same route builder for fluent registration.</returns>
public static IEndpointRouteBuilder MapTranslationImportEndpoint(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"/{HttpConstants.ApiPrefix}admin/localization")
            .WithTags("Admin - Localization");

        group.MapPost("/translations/import", ImportTranslations)
            .WithName("ImportTranslations");

        return endpoints;
    }

    /// <summary>
    /// Converts domain failures to HTTP 400 responses and unexpected exceptions to HTTP 500 responses.
    /// </summary>
    /// <remarks>
    /// Successful responses include aggregate import counts and per-entry errors produced by
    /// the import service. The endpoint itself does not add an authorization policy.
    /// </remarks>
    private static async Task<IResult> ImportTranslations(
        [FromBody] TranslationImportFileRequest request,
        [FromServices] ITranslationImportService importService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(TranslationImportEndpoint));

        try
        {
            var result = await importService.ImportAsync(request, cancellationToken);

            if (result is Result<TranslationImportResult, AeroError>.Failure failure)
            {
                logger.LogWarning("Translation import failed: {Error}", failure.Error);
                return TypedResults.BadRequest(new { error = failure.Error.ToString() });
            }

            if (result is Result<TranslationImportResult, AeroError>.Ok ok)
            {
                logger.LogInformation(
                    "Translation import completed: {Imported} imported, {Updated} updated, {Skipped} skipped, {Errors} errors",
                    ok.Value.TotalImported,
                    ok.Value.TotalUpdated,
                    ok.Value.TotalSkipped,
                    ok.Value.Errors.Count);

                return TypedResults.Ok(ok.Value);
            }

            return TypedResults.BadRequest(new { error = "Unexpected result from translation import service" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing translations");
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
