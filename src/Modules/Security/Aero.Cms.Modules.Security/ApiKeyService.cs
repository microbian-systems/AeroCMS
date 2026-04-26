using Aero.Cms.Abstractions.Services;
using Aero.EfCore;
using Aero.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Aero.Auth.Services;
using System.Security.Cryptography;
using System.Text;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Implementation of IApiKeyService using EF Core for persistence and hashed keys for security.
/// </summary>
public sealed class ApiKeyService(
    AeroDbContext dbContext, 
    IApiKeyFactory apiKeyFactory,
    IApiKeyGenerator apiKeyGenerator) : IApiKeyService
{
    public async Task<long?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        // We store the SHA256 hash of the API key for security.
        var hash = HashKey(apiKey);
        
        var account = await dbContext.ApiAccounts
            .FirstOrDefaultAsync(x => x.ApiKey == hash && x.Enabled, cancellationToken);

        if (account != null)
        {
            return account.Id;
        }

        return null;
    }

    public async Task<string> CreateKeyAsync(long userId, string email, string? apiKey = null, CancellationToken cancellationToken = default)
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
            RefreshToken = Guid.NewGuid().ToString("N"),
            RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(30),
            CreateDate = DateTimeOffset.UtcNow,
            ModifiedDate = DateTimeOffset.UtcNow
        };

        // Check if account already exists to avoid unique constraint violations during re-seeding
        var existing = await dbContext.ApiAccounts.FindAsync([userId], cancellationToken);
        if (existing != null)
        {
            existing.ApiKey = secretHash;
            existing.ModifiedDate = DateTimeOffset.UtcNow;
            dbContext.ApiAccounts.Update(existing);
        }
        else
        {
            dbContext.ApiAccounts.Add(account);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Return the RAW key only once
        return finalApiKey;
    }

    private static string HashKey(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}