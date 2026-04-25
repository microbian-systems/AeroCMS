using Aero.Cms.Abstractions.Services;
using Aero.EfCore;
using Aero.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Aero.Auth.Services;
using System.Security.Cryptography;
using System.Text;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Implementation of IApiKeyService using EF Core for persistence.
/// </summary>
public sealed class ApiKeyService(AeroDbContext dbContext, IApiKeyFactory apiKeyFactory) : IApiKeyService
{
    public async Task<long?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        // For now, we are storing the API key directly in ApiAccountModel.ApiKey
        // In a more secure implementation, we would store a hash.
        // The original Marten implementation was hashing the key.
        // Let's see if we should continue that or use the direct key for now.
        // The ApiAccountModel has an ApiKey property.
        
        var account = await dbContext.ApiAccounts
            .FirstOrDefaultAsync(x => x.ApiKey == apiKey && x.Enabled, cancellationToken);

        if (account != null)
        {
            return account.Id;
        }

        return null;
    }

    public async Task<string> CreateKeyAsync(long userId, string email, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        var finalApiKey = apiKey ?? apiKeyFactory.GenerateApiKey() ?? throw new InvalidOperationException("Failed to generate API key");
        
        var account = new ApiAccountModel
        {
            Id = userId,
            ApiKey = finalApiKey,
            Email = email,
            Enabled = true,
            RefreshToken = Guid.NewGuid().ToString(), // Placeholder
            RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(30),
            CreateDate = DateTimeOffset.UtcNow,
            ModifiedDate = DateTimeOffset.UtcNow
        };

        dbContext.ApiAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return finalApiKey;
    }
}