using System.Security.Cryptography;
using System.Text;
using Aero.Auth.Services;
using Aero.Cms.Abstractions.Security;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Creates and validates hashed, independently scoped API-key documents.
/// </summary>
public sealed class ApiKeyService(
    IDocumentSession session,
    IApiKeyGenerator apiKeyGenerator,
    ILogger<ApiKeyService> logger) : IApiKeyService
{
    private const int MaximumNameLength = 100;

    /// <inheritdoc />
    public async Task<AeroApiKeyValidation?> ValidateAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var secretHash = HashKey(apiKey);
        var key = await session.Query<ApiKeyDocument>()
            .FirstOrDefaultAsync(candidate => candidate.SecretHash == secretHash, cancellationToken);
        if (key is null || !key.IsActive)
            return null;

        key.LastUsedAt = DateTimeOffset.UtcNow;
        key.ModifiedOn = key.LastUsedAt;
        key.ModifiedBy = "api-key-validation";
        session.Store(key);
        await session.SaveChangesAsync(cancellationToken);

        return ToValidation(key);
    }

    /// <inheritdoc />
    public async Task<string> CreateKeyAsync(
        long userId,
        string email,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        _ = email;

        var generated = Generate(apiKey, ApiKeyEnvironment.Live);
        var now = DateTimeOffset.UtcNow;
        var key = new ApiKeyDocument
        {
            UserId = userId,
            CredentialKind = AeroApiKeyCredentialKind.UserSession,
            SecretHash = generated.SecretHash,
            Name = "Headless sign-in",
            Environment = ApiKeyEnvironment.Live,
            ExpiresAt = now.AddMinutes(15),
            CreatedOn = now,
            CreatedBy = userId.ToString(),
            ModifiedOn = now,
            ModifiedBy = userId.ToString()
        };

        session.Store(key);
        await session.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Created user-session API key {ApiKeyId} for user {UserId}",
            key.Id,
            userId);
        return generated.RawApiKey;
    }

    /// <inheritdoc />
    public async Task<IssuedApiKey> CreateScopedKeyAsync(
        CreateScopedApiKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateScopedRequest(request);

        var environment = request.IsTest ? ApiKeyEnvironment.Test : ApiKeyEnvironment.Live;
        var generated = Generate(null, environment);
        var now = DateTimeOffset.UtcNow;
        var key = new ApiKeyDocument
        {
            UserId = request.UserId,
            TenantId = request.TenantId,
            AllowedSiteIds = request.AllowedSiteIds
                .Where(siteId => siteId > 0)
                .Distinct()
                .Order()
                .ToList(),
            CredentialKind = AeroApiKeyCredentialKind.Service,
            SecretHash = generated.SecretHash,
            Name = request.Name.Trim(),
            Environment = environment,
            McpServer = request.McpServer,
            IsAdministrator = request.IsAdministrator,
            Permissions = NormalizePermissions(request.Permissions),
            ExpiresAt = request.ExpiresAt,
            CreatedOn = now,
            CreatedBy = request.CreatedBy,
            ModifiedOn = now,
            ModifiedBy = request.CreatedBy
        };

        session.Store(key);
        await session.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Created scoped API key {ApiKeyId} for user {UserId} tenant {TenantId}",
            key.Id,
            key.UserId,
            key.TenantId);
        return new IssuedApiKey(key.Id, generated.RawApiKey);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiKeySummary>> ListAsync(
        long userId,
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || tenantId <= 0)
            return [];

        var keys = await session.Query<ApiKeyDocument>()
            .Where(key => key.UserId == userId && key.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return keys
            .OrderByDescending(key => key.CreatedOn)
            .Select(ToSummary)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(
        long keyId,
        long userId,
        long tenantId,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (keyId <= 0 || userId <= 0 || tenantId <= 0)
            return false;

        var key = await session.LoadAsync<ApiKeyDocument>(keyId, cancellationToken);
        if (key is null || key.UserId != userId || key.TenantId != tenantId || key.RevokedAt is not null)
            return false;

        var now = DateTimeOffset.UtcNow;
        key.RevokedAt = now;
        key.ModifiedOn = now;
        key.ModifiedBy = revokedBy;
        key.RevokedByUserId = long.TryParse(revokedBy, out var revokerId) ? revokerId : null;
        session.Store(key);
        await session.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Revoked API key {ApiKeyId} for user {UserId} tenant {TenantId}",
            key.Id,
            key.UserId,
            key.TenantId);
        return true;
    }

    private (string RawApiKey, string SecretHash) Generate(
        string? callerSuppliedKey,
        ApiKeyEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(callerSuppliedKey))
        {
            var generated = apiKeyGenerator.Generate(environment);
            return (generated.RawApiKey, generated.SecretHash);
        }

        return (callerSuppliedKey, HashKey(callerSuppliedKey));
    }

    private static void ValidateScopedRequest(CreateScopedApiKeyRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.UserId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.TenantId);
        if (request.AllowedSiteIds.Count == 0 || request.AllowedSiteIds.Any(siteId => siteId <= 0))
            throw new ArgumentException("At least one valid site is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > MaximumNameLength)
            throw new ArgumentException($"Key name must contain 1 to {MaximumNameLength} characters.", nameof(request));
        if (request.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Key expiration must be in the future.", nameof(request));

        var permissions = NormalizePermissions(request.Permissions);
        if (request.McpServer &&
            !request.IsAdministrator &&
            !permissions.Any(permission => HasOperation(permission, 'R')))
        {
            throw new ArgumentException(
                "An MCP key must have at least one read permission or key-specific administrator access.",
                nameof(request));
        }
    }

    private static List<string> NormalizePermissions(IEnumerable<string> permissions)
    {
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var permission in permissions)
        {
            if (string.IsNullOrWhiteSpace(permission))
                continue;

            var parts = permission.Trim().Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !AeroApiKeyPermissionDomains.All.Contains(parts[0]))
            {
                throw new ArgumentException($"Unsupported API-key permission '{permission}'.", nameof(permissions));
            }

            var operations = parts[1]
                .ToUpperInvariant()
                .Where(operation => operation is 'C' or 'R' or 'U' or 'D')
                .Distinct()
                .Order()
                .ToArray();
            if (operations.Length == 0 || operations.Length != parts[1].Length)
                throw new ArgumentException($"Unsupported API-key permission '{permission}'.", nameof(permissions));

            normalized.Add($"{parts[0]}:{new string(operations)}");
        }

        return [.. normalized];
    }

    private static bool HasOperation(string permission, char operation)
    {
        var separator = permission.IndexOf(':');
        return separator > 0 &&
               permission.AsSpan(separator + 1).Contains(operation);
    }

    private static AeroApiKeyValidation ToValidation(ApiKeyDocument key) =>
        new(
            key.Id,
            key.UserId,
            key.CredentialKind,
            key.TenantId,
            key.AllowedSiteIds.ToArray(),
            key.McpServer,
            key.IsAdministrator,
            key.Permissions.ToArray(),
            key.ExpiresAt);

    private static ApiKeySummary ToSummary(ApiKeyDocument key) =>
        new(
            key.Id,
            key.UserId,
            key.TenantId,
            key.AllowedSiteIds.ToArray(),
            key.Name,
            key.McpServer,
            key.IsAdministrator,
            key.Permissions.ToArray(),
            key.CreatedOn,
            key.ExpiresAt,
            key.LastUsedAt,
            key.RevokedAt);

    private static string HashKey(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
