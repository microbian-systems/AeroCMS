namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Resolves the one durable setup-owned recovery administrator.</summary>
public interface IRecoveryAdministratorAuthority
{
    /// <summary>
    /// Returns the authoritative Identity user identifier only when completed setup state contains
    /// a valid Snowflake identifier; otherwise returns <see langword="null"/> so callers fail closed.
    /// </summary>
    Task<long?> GetUserIdAsync(CancellationToken cancellationToken = default);
}
