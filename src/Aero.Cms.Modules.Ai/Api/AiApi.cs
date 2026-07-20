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

/// <summary>
/// Maps the administrative endpoints for AI settings, content enhancement, and translation.
/// </summary>
/// <remarks>
/// The endpoint group requires the host's default authorization policy. Content submitted for
/// enhancement or translation can be sent to the selected external AI provider.
/// </remarks>
public static class AiApi
{
        /// <summary>
    /// Adds the administrative AI endpoint group to an endpoint route builder.
    /// </summary>
    /// <param name="app">The route builder to update.</param>
    /// <remarks>
    /// Maps authenticated endpoints beneath <c>/api/admin/ai</c> for settings, provider choices,
    /// enhancement, and translation. No policy more specific than <c>RequireAuthorization()</c>
    /// is attached here.
    /// </remarks>
public static void MapAiApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/ai")
            .WithTags("Admin - AI")
            .RequireAuthorization();

        group.MapPost("/content/enhance", EnhanceContent)
            .WithName("EnhanceContent");

        group.MapPost("/content/translate", TranslateContent)
            .WithName("TranslateContent");

        group.MapGet("/settings", GetSettings)
            .WithName("GetAiSettings");

        group.MapPost("/settings", SaveSettings)
            .WithName("SaveAiSettings");

        group.MapGet("/providers/options", GetProviderOptions)
            .WithName("GetAiProviderOptions");
    }

    /// <summary>
    /// Validates a translation request, invokes the configured provider, and converts its railway result to HTTP.
    /// </summary>
    /// <param name="request">The cultures and CMS field content to translate.</param>
    /// <param name="validator">The request validator.</param>
    /// <param name="service">The translation service that performs the provider call.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">A token that observes request abortion.</param>
    /// <returns>An HTTP result containing a translation, validation details, or a mapped problem response.</returns>
    private static async Task<IResult> TranslateContent(
        [FromBody] TranslateDocumentRequest request,
        [FromServices] IValidator<TranslateDocumentRequest> validator,
        [FromServices] IAiContentTranslationService service,
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
        var result = await service.TranslateAsync(request, cancellationToken);
        var elapsed = TimeProvider.System.GetElapsedTime(started);

        return result switch
        {
            Result<TranslateDocumentResponse>.Ok ok => LogTranslationSuccessAndReturn(logger, ok.Value, elapsed),
            Result<TranslateDocumentResponse>.Failure failure => LogFailureAndReturn(logger, failure.Error, elapsed),
            _ => Results.Problem("Unexpected AI translation result.")
        };
    }

    /// <summary>
    /// Loads the manager-safe AI configuration.
    /// </summary>
    /// <param name="settingsStore">The persistent AI settings store.</param>
    /// <param name="cancellationToken">A token that observes request abortion.</param>
    /// <returns>An HTTP result containing settings without plaintext API keys, or a problem response.</returns>
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

    /// <summary>
    /// Records translation completion metadata and returns the response body.
    /// </summary>
    /// <param name="logger">The endpoint logger.</param>
    /// <param name="response">The successful translation response.</param>
    /// <param name="elapsed">The measured service-call duration.</param>
    /// <returns>An HTTP 200 result containing <paramref name="response"/>.</returns>
    /// <remarks>The log includes provider, model, elapsed time, and field count, but not translated content.</remarks>
    private static IResult LogTranslationSuccessAndReturn(
        ILogger logger,
        TranslateDocumentResponse response,
        TimeSpan elapsed)
    {
        logger.LogInformation(
            "AI translation completed. Provider={Provider} Model={Model} ElapsedMs={ElapsedMs} FieldCount={FieldCount}",
            response.Provider,
            response.Model,
            elapsed.TotalMilliseconds,
            response.TranslatedFields.Count);

        return TypedResults.Ok(response);
    }

    /// <summary>
    /// Persists an AI settings update and returns its manager-safe representation.
    /// </summary>
    /// <param name="request">The provider profiles and global settings to store.</param>
    /// <param name="settingsStore">The persistent AI settings store.</param>
    /// <param name="cancellationToken">A token that observes request abortion.</param>
    /// <returns>An HTTP result containing the saved settings, validation details, or a problem response.</returns>
    /// <remarks>Provider API keys in the request are write-only and are protected by the settings store before persistence.</remarks>
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

    /// <summary>
    /// Returns enabled, configured providers that the module currently permits for content operations.
    /// </summary>
    /// <param name="settingsStore">The persistent AI settings store.</param>
    /// <param name="cancellationToken">A token that observes request abortion.</param>
    /// <returns>An HTTP result containing provider choices or a mapped problem response.</returns>
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

    /// <summary>
    /// Validates an enhancement request, invokes the configured provider, and converts its railway result to HTTP.
    /// </summary>
    /// <param name="request">The CMS field, context, and user prompt to send for enhancement.</param>
    /// <param name="validator">The request validator.</param>
    /// <param name="service">The enhancement service that performs the provider call.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">A token that observes request abortion.</param>
    /// <returns>An HTTP result containing enhanced content, validation details, or a mapped problem response.</returns>
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
            Result<EnhanceContentResponse>.Ok ok => LogSuccessAndReturn(logger, ok.Value, elapsed),
            Result<EnhanceContentResponse>.Failure failure => LogFailureAndReturn(logger, failure.Error, elapsed),
            _ => Results.Problem("Unexpected AI enhancement result.")
        };
    }

    /// <summary>
    /// Records enhancement completion metadata and returns the response body.
    /// </summary>
    /// <param name="logger">The endpoint logger.</param>
    /// <param name="response">The successful enhancement response.</param>
    /// <param name="elapsed">The measured service-call duration.</param>
    /// <returns>An HTTP 200 result containing <paramref name="response"/>.</returns>
    /// <remarks>The log includes provider, model, elapsed time, and optional token counts, but not generated content.</remarks>
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

    /// <summary>
    /// Records failure metadata and converts the error to an HTTP problem result.
    /// </summary>
    /// <param name="logger">The endpoint logger.</param>
    /// <param name="error">The railway error to map.</param>
    /// <param name="elapsed">The measured service-call duration.</param>
    /// <returns>An HTTP result produced by <see cref="ToProblem"/>.</returns>
    /// <remarks>The log records the error type and elapsed time without logging the request or error message.</remarks>
    private static IResult LogFailureAndReturn(ILogger logger, AeroError error, TimeSpan elapsed)
    {
        logger.LogWarning(
            "AI enhancement failed after {ElapsedMs}ms. ErrorType={ErrorType}",
            elapsed.TotalMilliseconds,
            error.GetType().Name);

        return ToProblem(error);
    }

    /// <summary>
    /// Maps a domain error to validation details or an HTTP problem response.
    /// </summary>
    /// <param name="error">The error to map.</param>
    /// <returns>
    /// A 400, 404, or 504 response for recognized error categories; otherwise, a generic 502 response.
    /// </returns>
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
