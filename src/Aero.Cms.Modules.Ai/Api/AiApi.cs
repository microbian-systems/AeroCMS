using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Cms.Modules.Ai.Services;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Api;

public static class AiApi
{
    public static void MapAiApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/ai")
            .WithTags("Admin - AI")
            .RequireAuthorization();

        group.MapPost("/content/enhance", EnhanceContent)
            .WithName("EnhanceContent");

        group.MapGet("/settings", GetSettings)
            .WithName("GetAiSettings");

        group.MapPost("/settings", SaveSettings)
            .WithName("SaveAiSettings");

        group.MapGet("/providers/options", GetProviderOptions)
            .WithName("GetAiProviderOptions");
    }

    private static async Task<IResult> GetSettings(
        [FromServices] IAiSettingsStore settingsStore,
        CancellationToken cancellationToken)
    {
        var result = await settingsStore.GetConfigurationAsync(cancellationToken);
        return result switch
        {
            Result<AiSettingsConfiguration, AeroError>.Ok ok => TypedResults.Ok(ok.Value),
            Result<AiSettingsConfiguration, AeroError>.Failure failure => ToProblem(failure.Error),
            _ => Results.Problem("Unexpected AI settings result.")
        };
    }

    private static async Task<IResult> SaveSettings(
        [FromBody] SaveAiSettingsRequest request,
        [FromServices] IAiSettingsStore settingsStore,
        CancellationToken cancellationToken)
    {
        var result = await settingsStore.SaveConfigurationAsync(request, cancellationToken);
        return result switch
        {
            Result<AiSettingsConfiguration, AeroError>.Ok ok => TypedResults.Ok(ok.Value),
            Result<AiSettingsConfiguration, AeroError>.Failure failure => ToProblem(failure.Error),
            _ => Results.Problem("Unexpected AI settings result.")
        };
    }

    private static async Task<IResult> GetProviderOptions(
        [FromServices] IAiSettingsStore settingsStore,
        CancellationToken cancellationToken)
    {
        var result = await settingsStore.GetProviderOptionsAsync(cancellationToken);
        return result switch
        {
            Result<IReadOnlyList<AiProviderOption>, AeroError>.Ok ok => TypedResults.Ok(ok.Value),
            Result<IReadOnlyList<AiProviderOption>, AeroError>.Failure failure => ToProblem(failure.Error),
            _ => Results.Problem("Unexpected AI provider options result.")
        };
    }

    private static async Task<IResult> EnhanceContent(
        [FromBody] EnhanceContentRequest request,
        [FromServices] IValidator<EnhanceContentRequest> validator,
        [FromServices] IAiContentEnhancementService service,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(AiApi));
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var started = TimeProvider.System.GetTimestamp();
        var result = await service.EnhanceAsync(request, cancellationToken);
        var elapsed = TimeProvider.System.GetElapsedTime(started);

        return result switch
        {
            Result<EnhanceContentResponse, AeroError>.Ok ok => LogSuccessAndReturn(logger, ok.Value, elapsed),
            Result<EnhanceContentResponse, AeroError>.Failure failure => LogFailureAndReturn(logger, failure.Error, elapsed),
            _ => Results.Problem("Unexpected AI enhancement result.")
        };
    }

    private static IResult LogSuccessAndReturn(
        ILogger logger,
        EnhanceContentResponse response,
        TimeSpan elapsed)
    {
        logger.LogInformation(
            "AI enhancement completed. Provider={Provider} Model={Model} ElapsedMs={ElapsedMs} InputTokens={InputTokens} OutputTokens={OutputTokens}",
            response.Provider,
            response.Model,
            elapsed.TotalMilliseconds,
            response.Usage?.InputTokens,
            response.Usage?.OutputTokens);

        return TypedResults.Ok(response);
    }

    private static IResult LogFailureAndReturn(ILogger logger, AeroError error, TimeSpan elapsed)
    {
        logger.LogWarning(
            "AI enhancement failed after {ElapsedMs}ms. ErrorType={ErrorType}",
            elapsed.TotalMilliseconds,
            error.GetType().Name);

        return ToProblem(error);
    }

    private static IResult ToProblem(AeroError error)
        => error switch
        {
            AeroError.Validation validation => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["request"] = validation.Errors.ToArray() }),
            AeroError.Configuration configuration => Results.Problem(configuration.msg, statusCode: StatusCodes.Status400BadRequest),
            AeroError.BadRequest badRequest => Results.Problem(badRequest.msg, statusCode: StatusCodes.Status400BadRequest),
            AeroError.InvalidRequest invalidRequest => Results.Problem(invalidRequest.msg, statusCode: StatusCodes.Status400BadRequest),
            AeroError.NotFound notFound => Results.Problem(notFound.msg, statusCode: StatusCodes.Status404NotFound),
            AeroError.Timeout timeout => Results.Problem(timeout.msg, statusCode: StatusCodes.Status504GatewayTimeout),
            _ => Results.Problem("AI request failed.", statusCode: StatusCodes.Status502BadGateway)
        };
}
