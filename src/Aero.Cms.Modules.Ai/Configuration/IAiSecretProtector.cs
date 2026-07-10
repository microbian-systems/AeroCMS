namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Defines an interface for IAiSecretProtector.
/// </summary>
public interface IAiSecretProtector
{
        /// <summary>
    /// Protect method.
    /// </summary>
string Protect(string secret);

        /// <summary>
    /// Unprotect method.
    /// </summary>
string Unprotect(string protectedSecret);
}
