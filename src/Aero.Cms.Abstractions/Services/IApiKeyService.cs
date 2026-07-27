using Aero.Cms.Abstractions.Security;

namespace Aero.Cms.Abstractions.Services;

/// <summary>
/// Service for validating and managing API keys.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Validates an API key and returns its key-scoped identity and capabilities.
    /// </summary>
    /// <param name="apiKey">The raw API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validated key state if valid; otherwise null.</returns>
    Task<AeroApiKeyValidation?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new API key for the specified user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="email">The email associated with the account.</param>
    /// <param name="apiKey">Optional pre-defined API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw API key.</returns>
    Task<string> CreateKeyAsync(long userId, string email, string? apiKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an explicitly tenant- and site-scoped service key.
    /// </summary>
    Task<IssuedApiKey> CreateScopedKeyAsync(
        CreateScopedApiKeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists safe key metadata for an owning user inside one tenant.
    /// </summary>
    Task<IReadOnlyList<ApiKeySummary>> ListAsync(
        long userId,
        long tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes one key owned by the specified user and tenant.
    /// </summary>
    Task<bool> RevokeAsync(
        long keyId,
        long userId,
        long tenantId,
        string revokedBy,
        CancellationToken cancellationToken = default);
}
