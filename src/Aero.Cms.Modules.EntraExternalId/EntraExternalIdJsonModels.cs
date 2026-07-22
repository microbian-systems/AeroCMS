using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.EntraExternalId;

internal sealed record EntraCorrelation(long BindingId, long TenantId, long SiteId, string CallbackUri,
    string Authority, string ClientIdDigest, string Nonce, string Verifier);

internal sealed record EntraTokenResponse(string? id_token);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EntraCorrelation))]
[JsonSerializable(typeof(EntraTokenResponse))]
internal partial class EntraExternalIdJsonContext : JsonSerializerContext;
