using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.WorkOS;

internal sealed record WorkOsAuthenticateRequest(string client_id, string client_secret, string grant_type, string code,
    string code_verifier, string? ip_address, string? user_agent);
internal sealed record WorkOsAuthenticateResponse(WorkOsUser? user, string? organization_id, string? access_token,
    JsonElement? impersonator);
internal sealed record WorkOsUser(string? id, string? email, bool? email_verified, string? first_name, string? last_name);
internal sealed record WorkOsCorrelation(long BindingId, long TenantId, long SiteId, string CallbackUri, string Verifier);
internal sealed record WorkOsJwtPayload(string? sid);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(WorkOsAuthenticateRequest))]
[JsonSerializable(typeof(WorkOsAuthenticateResponse))]
[JsonSerializable(typeof(WorkOsCorrelation))]
[JsonSerializable(typeof(WorkOsJwtPayload))]
internal partial class WorkOsJsonContext : JsonSerializerContext;
