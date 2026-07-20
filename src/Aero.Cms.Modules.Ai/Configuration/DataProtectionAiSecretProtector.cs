using Microsoft.AspNetCore.DataProtection;

namespace Aero.Cms.Modules.Ai.Configuration;

/// <summary>
/// Protects AI provider secrets with ASP.NET Core Data Protection.
/// </summary>
/// <param name="dataProtectionProvider">The provider used to create the module-specific data protector.</param>
/// <remarks>
/// Protected values are prefixed with <c>dp:</c> for storage identification and use the purpose
/// <c>Aero.Cms.Modules.Ai.ProviderKeys.v1</c>. Recoverability and cryptographic protection depend on
/// the host's Data Protection key-ring configuration and access controls.
/// </remarks>
public sealed class DataProtectionAiSecretProtector(IDataProtectionProvider dataProtectionProvider) : IAiSecretProtector
{
    private const string Prefix = "dp:";
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Aero.Cms.Modules.Ai.ProviderKeys.v1");

    /// <inheritdoc />
public string Protect(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return $"{Prefix}{protector.Protect(secret)}";
    }

    /// <inheritdoc />
public string Unprotect(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);

        var value = protectedSecret.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedSecret[Prefix.Length..]
            : protectedSecret;

        return protector.Unprotect(value);
    }
}
