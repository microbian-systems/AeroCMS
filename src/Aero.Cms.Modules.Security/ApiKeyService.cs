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
/// Validates and creates API-account credentials stored through an AeroDB document session.
/// </summary>
/// <param name="session">The document session used to query and upsert API-account records.</param>
/// <param name="apiKeyFactory">
/// The API-key factory accepted for dependency-injection compatibility. The current implementation does not capture
/// or call it.
/// </param>
/// <param name="apiKeyGenerator">The generator used when no caller-supplied key is provided.</param>
/// <param name="log">The logger that receives new-account details at debug level.</param>
/// <remarks>
/// <para>
/// Caller-supplied keys are stored as lowercase SHA-256 digests; generated keys use the digest supplied by
/// <c>IApiKeyGenerator</c>. Hashing is not encryption. The equality query can return the first of multiple enabled
/// records with the same digest because the configured index is not unique.
/// </para>
/// <para>
/// Creation treats only <see langword="null"/> and the empty string as absent, whereas validation rejects every
/// whitespace-only value. A nonempty whitespace-only key is therefore accepted, hashed, stored, and returned by
/// <see cref="CreateKeyAsync"/> but can never pass <see cref="ValidateAsync"/>.
/// </para>
/// <para>
/// This service does not apply authorization, tenant scope, API-key expiration, rate limiting, key-history retention,
/// or constant-time comparison beyond the database equality query.
/// </para>
/// </remarks>
public sealed class ApiKeyService(
    IDocumentSession session,
    IApiKeyFactory apiKeyFactory,
    IApiKeyGenerator apiKeyGenerator,
    ILogger<ApiKeyService> log) : IApiKeyService
{
    /// <summary>
    /// Hashes a non-blank key and returns the identifier of the first enabled matching API-account document.
    /// </summary>
    /// <param name="apiKey">The raw API key to hash. Null, empty, and whitespace-only values are rejected.</param>
    /// <param name="cancellationToken">The token forwarded to the AeroDB query.</param>
    /// <returns>
    /// The identifier of the first enabled account whose stored digest matches, or <see langword="null"/> for blank
    /// input or no match.
    /// </returns>
    /// <remarks>
    /// The lookup checks only the digest and <c>Enabled</c> flag. It does not enforce uniqueness, load the Identity
    /// user, check its active/deleted state, apply tenant scope, or inspect either refresh-token representation.
    /// Query and cancellation failures propagate.
    /// </remarks>
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
    /// Generates a key when <paramref name="apiKey"/> is null or empty, otherwise hashes the supplied value exactly,
    /// upserts the API-account document keyed by <paramref name="userId"/>, commits, and returns the raw key.
    /// </summary>
    /// <param name="userId">The account document identifier and audit actor value.</param>
    /// <param name="email">The email written only when a new account document is created.</param>
    /// <param name="apiKey">
    /// An optional raw key. Any nonempty value, including whitespace-only input, is accepted without normalization.
    /// </param>
    /// <param name="ct">The cancellation token forwarded to the load and save operations.</param>
    /// <returns>The generated or caller-supplied raw API key after the document session saves successfully.</returns>
    /// <remarks>
    /// <para>
    /// A new account is enabled and receives a plaintext GUID-formatted value in
    /// <c>ApiAccountModel.RefreshToken</c> with an expiry 30 days after creation. That legacy account field is separate
    /// from the hashed <c>RefreshToken</c> documents managed by <c>IRefreshTokenService</c>. It is not issued,
    /// validated, rotated, or consulted by <c>POST /api/v1/jwt/refresh</c>.
    /// </para>
    /// <para>
    /// Before a new account is stored, the complete <c>ApiAccountModel</c> is serialized and passed as one value to a
    /// debug log message. The payload includes the plaintext <c>RefreshToken</c>, email address, API-key digest, user
    /// identifier, enabled flag, refresh expiry, claims collection, and audit fields. Argument evaluation performs
    /// this serialization even when a provider later filters out debug events. The raw API key is not a property of
    /// that model, but it is returned to the caller. Hosts must treat both the returned key and this log event as
    /// sensitive and are responsible for filtering the entire event and for sink redaction, access control, transport,
    /// and retention.
    /// </para>
    /// <para>
    /// For an existing account, only <c>ApiKey</c> and <c>ModifiedOn</c> are changed; email, enabled state, legacy
    /// refresh token and expiry, claims, and audit actor fields are left as stored. The prior key digest is overwritten
    /// without history or an explicit revocation record.
    /// </para>
    /// <para>
    /// The method does not validate the user identifier, email, or caller-supplied key. In particular, a nonempty
    /// whitespace-only key is saved but is unusable because <see cref="ValidateAsync"/> rejects it before hashing.
    /// Generation, persistence, serialization, and cancellation failures propagate without rollback guarantees beyond
    /// those of the document session.
    /// </para>
    /// </remarks>
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
