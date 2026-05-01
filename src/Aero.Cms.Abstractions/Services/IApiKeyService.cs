namespace Aero.Cms.Abstractions.Services;

/// <summary>
/// Service for validating and managing API keys.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Validates an API key and returns the associated user's unique identifier.
    /// </summary>
    /// <param name="apiKey">The raw API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user ID if valid; otherwise null.</returns>
    Task<long?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new API key for the specified user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="email">The email associated with the account.</param>
    /// <param name="apiKey">Optional pre-defined API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw API key.</returns>
    Task<string> CreateKeyAsync(long userId, string email, string? apiKey = null, CancellationToken cancellationToken = default);
}
