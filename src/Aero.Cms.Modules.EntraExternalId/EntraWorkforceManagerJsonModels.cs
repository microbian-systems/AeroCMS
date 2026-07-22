using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.EntraExternalId;

internal sealed record EntraWorkforceManagerCorrelation(
    long BindingId,
    string Provider,
    string Authority,
    string OrganizationId,
    string CallbackUri,
    string Purpose,
    string ClientIdDigest,
    string Nonce,
    string Verifier);

internal sealed record EntraWorkforceTokenResponse(string? id_token);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(EntraWorkforceManagerCorrelation))]
[JsonSerializable(typeof(EntraWorkforceTokenResponse))]
internal partial class EntraWorkforceManagerJsonContext : JsonSerializerContext;
