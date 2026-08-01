using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Commerce.A2A.Models;

/// <summary>Public, anonymous A2A agent card for the site-scoped Commerce catalog.</summary>
public sealed record A2AAgentCard(
    string Name,
    string Description,
    IReadOnlyList<A2AAgentInterface> SupportedInterfaces,
    string Version,
    A2AAgentCapabilities Capabilities,
    IReadOnlyList<string> DefaultInputModes,
    IReadOnlyList<string> DefaultOutputModes,
    IReadOnlyList<A2AAgentSkill> Skills);

/// <summary>Describes one A2A protocol binding exposed by the agent.</summary>
public sealed record A2AAgentInterface(string Url, string ProtocolBinding, string ProtocolVersion);

/// <summary>Describes the optional A2A capabilities supported by this read-only agent.</summary>
public sealed record A2AAgentCapabilities(bool Streaming, bool PushNotifications, bool ExtendedAgentCard);

/// <summary>Describes an anonymous, read-only catalog skill.</summary>
public sealed record A2AAgentSkill(string Id, string Name, string Description, IReadOnlyList<string> Tags, IReadOnlyList<string> Examples);

/// <summary>Represents a JSON-RPC 2.0 request accepted by the Commerce A2A endpoint.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record A2AJsonRpcRequest(string? Jsonrpc, JsonElement? Id, string? Method, JsonElement? Params);

/// <summary>Represents the standard A2A SendMessage request parameters.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record A2ASendMessageRequest(
    A2AInboundMessage? Message,
    JsonElement? Configuration,
    JsonElement? Metadata,
    string? Tenant);

/// <summary>Represents the inbound A2A message shape used by the read-only catalog agent.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record A2AInboundMessage(
    string? MessageId,
    string? ContextId,
    string? TaskId,
    string? Role,
    IReadOnlyList<A2AInboundPart>? Parts,
    IReadOnlyList<string>? Extensions,
    JsonElement? Metadata,
    IReadOnlyList<string>? ReferenceTaskIds);

/// <summary>Represents the one structured data part accepted by the catalog agent.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record A2AInboundPart(
    JsonElement? Data,
    string? MediaType,
    string? Filename,
    JsonElement? Metadata,
    string? Raw,
    string? Text,
    string? Url);

/// <summary>Represents the operation envelope carried by the inbound A2A data part.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record A2ACatalogSkillInvocation(string? SkillId, JsonElement? Input);

/// <summary>Bounded input for the public product-search skill.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record A2ASearchProductsInput(string? Query, string? Category, int? Skip, int? Take);

/// <summary>Bounded input for the public product lookup skill.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record A2AGetProductInput(string? Slug);

/// <summary>
/// Anonymous A2A catalog projection. This is deliberately independent from the storefront contract so
/// storefront-only merchandising or subscription fields cannot become part of the agent protocol.
/// </summary>
public sealed record A2APublicListing(
    long Id,
    string Slug,
    string Name,
    string? ShortDescription,
    string? Description,
    string? Category,
    string? ImageUrl,
    decimal Price,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? CompareAtPrice,
    string Currency,
    bool IsFeatured);

/// <summary>Public output of the product-search skill.</summary>
public sealed record A2ASearchProductsOutput(IReadOnlyList<A2APublicListing> Products, long TotalCount);

/// <summary>Public output of the product lookup skill.</summary>
public sealed record A2AGetProductOutput(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] A2APublicListing? Product);

/// <summary>Wraps the direct synchronous A2A response message.</summary>
public sealed record A2ASendMessageResponse(A2AOutboundMessage Message);

/// <summary>Represents a direct response message from the catalog agent.</summary>
public sealed record A2AOutboundMessage(string MessageId, string ContextId, string Role, IReadOnlyList<A2AOutboundPart> Parts);

/// <summary>Represents a structured JSON data part in the catalog agent response.</summary>
public sealed record A2AOutboundPart(JsonElement Data, string MediaType);

/// <summary>Represents a JSON-RPC success response.</summary>
public sealed record A2AJsonRpcSuccess(JsonElement Id, A2ASendMessageResponse Result)
{
    public string Jsonrpc { get; init; } = "2.0";
}

/// <summary>Represents a JSON-RPC error response.</summary>
public sealed record A2AJsonRpcFailure(JsonElement? Id, A2AJsonRpcError Error)
{
    public string Jsonrpc { get; init; } = "2.0";
}

/// <summary>Represents one safe JSON-RPC error.</summary>
public sealed record A2AJsonRpcError(int Code, string Message);

/// <summary>Source-generated JSON metadata for the public Commerce A2A protocol surface.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(A2AAgentCard))]
[JsonSerializable(typeof(A2AJsonRpcRequest))]
[JsonSerializable(typeof(A2ASendMessageRequest))]
[JsonSerializable(typeof(A2ACatalogSkillInvocation))]
[JsonSerializable(typeof(A2ASearchProductsInput))]
[JsonSerializable(typeof(A2AGetProductInput))]
[JsonSerializable(typeof(A2AJsonRpcSuccess))]
[JsonSerializable(typeof(A2AJsonRpcFailure))]
[JsonSerializable(typeof(A2ASearchProductsOutput))]
[JsonSerializable(typeof(A2AGetProductOutput))]
public partial class A2ACommerceJsonContext : JsonSerializerContext;
