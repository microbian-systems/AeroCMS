namespace Aero.Cms.Abstractions.Security;

/// <summary>
/// Stable claim names emitted only from validated Aero API-key state.
/// </summary>
public static class AeroApiKeyClaimTypes
{
    public const string KeyId = "aero.api_key_id";
    public const string CredentialKind = "aero.api_key_kind";
    public const string TenantId = "aero.tenant_id";
    public const string SiteId = "aero.site_id";
    public const string McpServer = "aero.mcp_server";
    public const string Administrator = "aero.api_key_admin";
    public const string Permission = "aero.permission";
}

/// <summary>
/// Authentication and authorization names for direct API-key requests.
/// </summary>
public static class AeroApiKeyAuthenticationDefaults
{
    public const string Scheme = "AeroApiKey";
    public const string McpPolicy = "AeroApiKey.Mcp";
    public const string HeaderName = "X-Aero-Api-Key";
    public const string SiteHeaderName = "X-Aero-Site-Id";
    public const string AuthorizationPrefix = "ApiKey ";
}

/// <summary>
/// Distinguishes short-lived user-login bootstrap keys from explicitly scoped service keys.
/// </summary>
public enum AeroApiKeyCredentialKind
{
    UserSession = 0,
    Service = 1
}

/// <summary>
/// Canonical MCP permission domains.
/// </summary>
public static class AeroApiKeyPermissionDomains
{
    public const string Sites = "sites";
    public const string Pages = "pages";
    public const string Posts = "posts";
    public const string Docs = "docs";
    public const string ContentTypes = "content-types";
    public const string ContentItems = "content-items";
    public const string Commerce = "commerce";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            Sites,
            Pages,
            Posts,
            Docs,
            ContentTypes,
            ContentItems,
            Commerce
        ],
        StringComparer.Ordinal);
}

/// <summary>
/// Validated, server-derived identity and capability state for one API key.
/// </summary>
public sealed record AeroApiKeyValidation(
    long KeyId,
    long UserId,
    AeroApiKeyCredentialKind CredentialKind,
    long TenantId,
    IReadOnlyList<long> AllowedSiteIds,
    bool McpServer,
    bool IsAdministrator,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Explicit request used to issue a tenant- and site-scoped service key.
/// </summary>
public sealed record CreateScopedApiKeyRequest(
    long UserId,
    long TenantId,
    IReadOnlyList<long> AllowedSiteIds,
    string Name,
    bool IsTest,
    bool McpServer,
    bool IsAdministrator,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? ExpiresAt,
    string CreatedBy);

/// <summary>
/// Returns a newly created key identifier and the raw secret, which is shown only once.
/// </summary>
public sealed record IssuedApiKey(long KeyId, string RawApiKey);

/// <summary>
/// Safe API-key metadata returned by management endpoints.
/// </summary>
public sealed record ApiKeySummary(
    long KeyId,
    long UserId,
    long TenantId,
    IReadOnlyList<long> AllowedSiteIds,
    string Name,
    bool McpServer,
    bool IsAdministrator,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);
