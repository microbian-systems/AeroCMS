using Microsoft.AspNetCore.DataProtection;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Represents a class for DataProtectionAiSecretProtector.
/// </summary>
public sealed class DataProtectionAiSecretProtector(IDataProtectionProvider dataProtectionProvider) : IAiSecretProtector
{
    private const string Prefix = "dp:";
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Aero.Cms.Modules.Ai.ProviderKeys.v1");

        /// <summary>
    /// Protect method.
    /// </summary>
public string Protect(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return $"{Prefix}{protector.Protect(secret)}";
    }

        /// <summary>
    /// Unprotect method.
    /// </summary>
public string Unprotect(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);

        var value = protectedSecret.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedSecret[Prefix.Length..]
            : protectedSecret;

        return protector.Unprotect(value);
    }
}
