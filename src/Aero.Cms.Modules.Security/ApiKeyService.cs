using Aero.Cms.Abstractions.Services;
using Aero.Models.Entities;
using Aero.Auth.Services;
using AeroDB.Sable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Aero.Core.Extensions;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Implementation of IApiKeyService using AeroDB.Sable for persistence and hashed keys for security.
/// </summary>
public sealed class ApiKeyService(
    IDocumentSession session,
    IApiKeyFactory apiKeyFactory,
    IApiKeyGenerator apiKeyGenerator,
    ILogger<ApiKeyService> log) : IApiKeyService
{
        /// <summary>
    /// ValidateAsync method.
    /// </summary>
public async Task<long?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        // We store the SHA256 hash of the API key for security.
        var hash = HashKey(apiKey);
        
        var account = await session.Query<ApiAccountModel>()
            .FirstOrDefaultAsync(x => x.ApiKey == hash && x.Enabled, cancellationToken);

        if (account != null)
        {
            return account.Id;
        }

        return null;
    }

        /// <summary>
    /// CreateKeyAsync method.
    /// </summary>
public async Task<string> CreateKeyAsync(long userId, string email, string? apiKey = null, CancellationToken ct = default)
    {
        string finalApiKey;
        string secretHash;

        if (string.IsNullOrEmpty(apiKey))
        {
            // Use the advanced generator for sk_live/sk_test style keys
            var generated = apiKeyGenerator.Generate(ApiKeyEnvironment.Live);
            finalApiKey = generated.RawApiKey;
            secretHash = generated.SecretHash;
        }
        else
        {
            // Use provided key (e.g. from seeding) and hash it
            finalApiKey = apiKey;
            secretHash = HashKey(finalApiKey);
        }

        var account = new ApiAccountModel
        {
            Id = userId,
            ApiKey = secretHash, // Store the hash
            Email = email,
            Enabled = true,
            RefreshToken = Guid.NewGuid().ToString("N"), // todo - verify Guid.NewGuid() is ok for a refresh token
            RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(30),
            CreatedBy = userId.ToString(),
            CreatedOn = DateTimeOffset.UtcNow,
            ModifiedBy = userId.ToString(),
            ModifiedOn = DateTimeOffset.UtcNow
        };

        // Check if account already exists to avoid unique constraint violations during re-seeding
        var existing = await session.LoadAsync<ApiAccountModel>(userId, ct);
        if (existing != null)
        {
            existing.ApiKey = secretHash;
            existing.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(existing);
        }
        else
        {
            log.LogDebug("creating new api account: {a}", account.ToJson());
            session.Store(account);
        }

        await session.SaveChangesAsync(ct);

        // Return the RAW key only once
        return finalApiKey;
    }

    private static string HashKey(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}