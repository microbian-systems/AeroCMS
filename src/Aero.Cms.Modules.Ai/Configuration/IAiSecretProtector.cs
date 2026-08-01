namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Protects and recovers AI provider secrets for persisted configuration.
/// </summary>
/// <remarks>
/// This abstraction defines reversible protection, not secret authorization, rotation, or external
/// secret-store behavior. Implementations determine the protection mechanism and key lifecycle.
/// </remarks>
public interface IAiSecretProtector
{
        /// <summary>
    /// Converts a plaintext secret to a representation suitable for the configured settings store.
    /// </summary>
    /// <param name="secret">The non-empty plaintext secret.</param>
    /// <returns>A protected representation of <paramref name="secret"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="secret"/> is null, empty, or white space.</exception>
string Protect(string secret);

        /// <summary>
    /// Recovers a plaintext secret from a protected representation.
    /// </summary>
    /// <param name="protectedSecret">The non-empty protected representation.</param>
    /// <returns>The recovered plaintext secret.</returns>
    /// <exception cref="ArgumentException"><paramref name="protectedSecret"/> is null, empty, or white space.</exception>
    /// <remarks>Implementations can throw additional exceptions when the value is invalid or its protection key is unavailable.</remarks>
string Unprotect(string protectedSecret);
}
