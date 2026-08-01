using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.WorkOS;

internal sealed record WorkOsManagerCorrelation(
    long BindingId,
    string Provider,
    string Authority,
    string OrganizationId,
    string CallbackUri,
    string Purpose,
    string ClientIdDigest,
    string Verifier);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(WorkOsManagerCorrelation))]
internal partial class WorkOsManagerJsonContext : JsonSerializerContext;
