using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aero.Cms.Modules.Commerce.A2A.Mappers;
using Aero.Cms.Modules.Commerce.A2A.Models;
using Aero.Cms.Modules.Commerce.A2A.Services;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.A2A.Api;

/// <summary>Maps the anonymous, host-resolved, read-only Commerce A2A surface.</summary>
public static class A2ACommerceEndpoints
{
    private const int MaxRequestBytes = 16 * 1024;
    private const string ProtocolVersion = "1.0";
    private const string JsonMediaType = "application/json";
    private const string SearchProductsSkill = "search_products";
    private const string GetProductSkill = "get_product";

    /// <summary>Maps agent discovery and the sole synchronous Commerce A2A operation.</summary>
    public static IEndpointRouteBuilder MapA2ACommerceApi(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("/.well-known/agent-card.json", GetAgentCardAsync).AllowAnonymous();
        builder.MapPost("/a2a/commerce", SendMessageAsync).AllowAnonymous();
        return builder;
    }

    private static async Task<IResult> GetAgentCardAsync(
        HttpContext context,
        ISiteContext site,
        IA2ASettingsService settings,
        CancellationToken ct)
    {
        // Discovery 404s must remain direct protocol responses. Otherwise the global
        // CMS status-page re-execution replaces both the body and this no-store policy.
        var statusCodePages = context.Features.Get<IStatusCodePagesFeature>();
        if (statusCodePages is not null)
            statusCodePages.Enabled = false;
        context.Response.Headers.CacheControl = "no-store";
        if (!await IsPublicA2AEnabledAsync(site, settings, ct) || !TryGetPublicEndpointUrl(context, out var endpointUrl))
            return Results.NotFound();

        var card = new A2AAgentCard(
            "Commerce Catalog Agent",
            "Anonymous read-only access to this site's published Commerce catalog.",
            [new A2AAgentInterface(endpointUrl, "JSONRPC", ProtocolVersion)],
            "1.0.0",
            new A2AAgentCapabilities(false, false, false),
            [JsonMediaType],
            [JsonMediaType],
            [
                new A2AAgentSkill(
                    SearchProductsSkill,
                    "Search products",
                    "Searches this site's published and active catalog listings in the current public culture.",
                    ["commerce", "catalog", "products", "search"],
                    ["{\"skillId\":\"search_products\",\"input\":{\"query\":\"tea\",\"take\":10}}"]),
                new A2AAgentSkill(
                    GetProductSkill,
                    "Get product",
                    "Gets one published and active catalog listing in the current public culture by canonical slug.",
                    ["commerce", "catalog", "products", "product"],
                    ["{\"skillId\":\"get_product\",\"input\":{\"slug\":\"green-tea\"}}"])
            ]);

        return Results.Json(card, A2ACommerceJsonContext.Default.A2AAgentCard, contentType: JsonMediaType);
    }

    private static async Task<IResult> SendMessageAsync(
        HttpContext context,
        ISiteContext site,
        IA2ASettingsService settings,
        IProductService products,
        CancellationToken ct)
    {
        // Public A2A is host-scoped. Check this before examining the client body so a disabled
        // site does not reveal parser behavior or any other Commerce capability.
        if (!await IsPublicA2AEnabledAsync(site, settings, ct))
            return Results.NotFound();

        if (!context.Request.HasJsonContentType())
            return RpcError(null, -32600, "Request content type must be application/json.", StatusCodes.Status400BadRequest);

        if (context.Request.ContentLength is > MaxRequestBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        if (!TryGetRequestedProtocolVersion(context, out var versionError))
            return RpcError(null, -32009, versionError!, StatusCodes.Status400BadRequest);

        var body = await ReadRequestAsync(context.Request, ct);
        if (body.Kind == A2ARequestBodyKind.TooLarge)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (body.Kind != A2ARequestBodyKind.Success || body.Request is null)
            return RpcError(null, -32700, "Invalid JSON payload.", StatusCodes.Status400BadRequest);

        var request = body.Request;
        if (!IsValidJsonRpcRequest(request, out var requestError))
            return RpcError(request.Id, -32600, requestError!, StatusCodes.Status400BadRequest);

        if (!string.Equals(request.Method, "SendMessage", StringComparison.Ordinal))
            return RpcError(request.Id, -32601, "Method not found.", StatusCodes.Status400BadRequest);

        string? sendError = null;
        string? invocationError = null;
        A2ACatalogSkillInvocation? invocation = null;
        var invocationErrorCode = -32602;
        var parsedSend = TryDeserialize(request.Params!.Value, A2ACommerceJsonContext.Default.A2ASendMessageRequest, out A2ASendMessageRequest? send, out sendError);
        var parsedInvocation = parsedSend && TryGetInvocation(send, out invocation, out invocationError, out invocationErrorCode);
        if (!parsedInvocation)
        {
            return RpcError(request.Id, parsedSend ? invocationErrorCode : -32602, sendError ?? invocationError!, StatusCodes.Status400BadRequest);
        }

        return invocation!.SkillId switch
        {
            SearchProductsSkill => await SearchProductsAsync(request.Id!.Value, invocation.Input!.Value, send!.Message!.ContextId, products, site, ct),
            GetProductSkill => await GetProductAsync(request.Id!.Value, invocation.Input!.Value, send!.Message!.ContextId, products, site, ct),
            _ => RpcError(request.Id, -32602, "The requested catalog skill is not available.", StatusCodes.Status400BadRequest)
        };
    }

    private static async Task<IResult> SearchProductsAsync(
        JsonElement id,
        JsonElement input,
        string? contextId,
        IProductService products,
        ISiteContext site,
        CancellationToken ct)
    {
        if (!TryDeserialize(input, A2ACommerceJsonContext.Default.A2ASearchProductsInput, out A2ASearchProductsInput? request, out var error) ||
            !ValidateSearch(request!, out error))
        {
            return RpcError(id, -32602, error!, StatusCodes.Status400BadRequest);
        }

        var result = await products.SearchPublishedAsync(
            site.TenantId,
            site.SiteId,
            CultureInfo.CurrentUICulture.Name,
            request!.Query,
            request.Category,
            request.Skip ?? 0,
            request.Take ?? 20,
            ct: ct);

        return result switch
        {
            Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok ok
                => RpcSuccess(id, A2APublicCatalogMapper.ToSearchOutput(ok.Value), contextId),
            _ => RpcError(id, -32603, "Catalog operation failed.", StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> GetProductAsync(
        JsonElement id,
        JsonElement input,
        string? contextId,
        IProductService products,
        ISiteContext site,
        CancellationToken ct)
    {
        if (!TryDeserialize(input, A2ACommerceJsonContext.Default.A2AGetProductInput, out A2AGetProductInput? request, out var error) ||
            !ValidateGetProduct(request!, out error))
        {
            return RpcError(id, -32602, error!, StatusCodes.Status400BadRequest);
        }

        var result = await products.GetPublishedListingBySlugAsync(
            site.TenantId,
            site.SiteId,
            CultureInfo.CurrentUICulture.Name,
            request!.Slug!,
            ct);

        return result switch
        {
            Result<ProductListingDocument?, AeroError>.Ok { Value: { } listing }
                => RpcSuccess(id, A2APublicCatalogMapper.ToProductOutput(listing), contextId),
            Result<ProductListingDocument?, AeroError>.Ok
                => RpcSuccess(id, A2APublicCatalogMapper.ToProductOutput(null), contextId),
            _ => RpcError(id, -32603, "Catalog operation failed.", StatusCodes.Status500InternalServerError)
        };
    }

    private static bool TryGetInvocation(
        A2ASendMessageRequest? request,
        out A2ACatalogSkillInvocation? invocation,
        out string? error,
        out int errorCode)
    {
        invocation = null;
        error = null;
        errorCode = -32602;
        var message = request?.Message;
        if (message is null ||
            string.IsNullOrWhiteSpace(message.MessageId) || message.MessageId.Length > 128 ||
            !string.Equals(message.Role, "ROLE_USER", StringComparison.Ordinal) ||
            message.Parts is not { Count: 1 })
        {
            error = "A ROLE_USER message with one data part is required.";
            return false;
        }

        if (!string.IsNullOrEmpty(message.TaskId))
        {
            errorCode = -32004;
            error = "This read-only catalog agent does not support task continuations.";
            return false;
        }

        var part = message.Parts[0];
        if (!IsJsonMediaType(part.MediaType))
        {
            errorCode = -32005;
            error = "The catalog agent supports application/json message parts only.";
            return false;
        }

        if (part.Data is not { ValueKind: JsonValueKind.Object } data ||
            !TryDeserialize(data, A2ACommerceJsonContext.Default.A2ACatalogSkillInvocation, out invocation, out error) ||
            invocation?.Input is not { ValueKind: JsonValueKind.Object } ||
            string.IsNullOrWhiteSpace(invocation.SkillId) || invocation.SkillId.Length > 64)
        {
            error ??= "The data part must contain one catalog skill invocation with a JSON object input.";
            invocation = null;
            return false;
        }

        return true;
    }

    private static bool IsJsonMediaType(string? mediaType)
        => !string.IsNullOrWhiteSpace(mediaType) &&
           string.Equals(mediaType.Split(';', 2)[0].Trim(), JsonMediaType, StringComparison.OrdinalIgnoreCase);

    private static bool ValidateSearch(A2ASearchProductsInput request, out string? error)
    {
        error = null;
        if (request.Query?.Trim().Length > 200)
            error = "Search query must be 200 characters or fewer.";
        else if (request.Category?.Trim().Length > 256)
            error = "Category must be 256 characters or fewer.";
        else if (request.Skip is < 0)
            error = "Skip must be zero or greater.";
        else if (request.Take is < 1 or > 100)
            error = "Take must be between 1 and 100.";
        return error is null;
    }

    private static bool ValidateGetProduct(A2AGetProductInput request, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Length > 160 || !CatalogSlug.IsCanonical(request.Slug))
            error = "A canonical product slug is required.";
        return error is null;
    }

    private static bool IsValidJsonRpcRequest(A2AJsonRpcRequest request, out string? error)
    {
        error = null;
        if (!string.Equals(request.Jsonrpc, "2.0", StringComparison.Ordinal) ||
            request.Id is not { } id || id.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) ||
            string.IsNullOrWhiteSpace(request.Method) || request.Method.Length > 64 ||
            request.Params is not { ValueKind: JsonValueKind.Object })
        {
            error = "Request payload validation error.";
            return false;
        }

        return true;
    }

    private static bool TryGetRequestedProtocolVersion(HttpContext context, out string? error)
    {
        error = null;
        var requested = context.Request.Headers["A2A-Version"].ToString();
        var effectiveVersion = string.IsNullOrWhiteSpace(requested) ? "0.3" : requested;
        if (!string.Equals(effectiveVersion, ProtocolVersion, StringComparison.Ordinal))
        {
            error = "Protocol version is not supported.";
            return false;
        }

        return true;
    }

    private static async Task<bool> IsPublicA2AEnabledAsync(ISiteContext site, IA2ASettingsService settings, CancellationToken ct)
    {
        if (site.TenantId <= 0 || site.SiteId <= 0)
            return false;

        var result = await settings.GetAsync(site.TenantId, site.SiteId, ct);
        return result is Result<A2ASettingsResponse, AeroError>.Ok { Value.IsEnabled: true };
    }

    private static bool TryGetPublicEndpointUrl(HttpContext context, out string endpointUrl)
    {
        endpointUrl = string.Empty;
        var host = context.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out _))
        {
            return false;
        }

        endpointUrl = $"https://{context.Request.Host}{context.Request.PathBase}/a2a/commerce";
        return Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static async Task<A2ARequestBody> ReadRequestAsync(HttpRequest request, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var read = await request.Body.ReadAsync(chunk.AsMemory(), ct);
            if (read == 0)
                break;
            if (buffer.Length + read > MaxRequestBytes)
                return A2ARequestBody.TooLarge;
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }

        if (buffer.Length == 0)
            return A2ARequestBody.Invalid;

        try
        {
            var value = JsonSerializer.Deserialize(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), A2ACommerceJsonContext.Default.A2AJsonRpcRequest);
            return value is null ? A2ARequestBody.Invalid : new A2ARequestBody(A2ARequestBodyKind.Success, value);
        }
        catch (JsonException)
        {
            return A2ARequestBody.Invalid;
        }
    }

    private static bool TryDeserialize<T>(JsonElement value, JsonTypeInfo<T> typeInfo, out T? output, out string? error)
    {
        output = default;
        error = null;
        try
        {
            output = JsonSerializer.Deserialize(value, typeInfo);
            return output is not null;
        }
        catch (JsonException)
        {
            error = "Request payload validation error.";
            return false;
        }
    }

    private static IResult RpcSuccess(JsonElement id, A2ASearchProductsOutput output, string? contextId)
        => RpcSuccess(id, JsonSerializer.SerializeToElement(output, A2ACommerceJsonContext.Default.A2ASearchProductsOutput), contextId);

    private static IResult RpcSuccess(JsonElement id, A2AGetProductOutput output, string? contextId)
        => RpcSuccess(id, JsonSerializer.SerializeToElement(output, A2ACommerceJsonContext.Default.A2AGetProductOutput), contextId);

    private static IResult RpcSuccess(JsonElement id, JsonElement data, string? contextId)
    {
        var message = new A2AOutboundMessage(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(contextId) ? Guid.NewGuid().ToString("N") : contextId,
            "ROLE_AGENT",
            [new A2AOutboundPart(data, JsonMediaType)]);
        var response = new A2AJsonRpcSuccess(id, new A2ASendMessageResponse(message));
        return Results.Json(response, A2ACommerceJsonContext.Default.A2AJsonRpcSuccess, contentType: JsonMediaType);
    }

    private static IResult RpcError(JsonElement? id, int code, string message, int statusCode)
        => Results.Json(new A2AJsonRpcFailure(id, new A2AJsonRpcError(code, message)), A2ACommerceJsonContext.Default.A2AJsonRpcFailure, statusCode: statusCode, contentType: JsonMediaType);

    private enum A2ARequestBodyKind { Invalid, Success, TooLarge }

    private sealed record A2ARequestBody(A2ARequestBodyKind Kind, A2AJsonRpcRequest? Request = null)
    {
        public static A2ARequestBody Invalid { get; } = new(A2ARequestBodyKind.Invalid);
        public static A2ARequestBody TooLarge { get; } = new(A2ARequestBodyKind.TooLarge);
    }
}
